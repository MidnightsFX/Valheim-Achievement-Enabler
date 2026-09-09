using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AchievementEnabler.Preloader
{
    internal sealed class RewriteResult
    {
        internal List<string> SummaryLines = new List<string>();
    }

    /// <summary>
    /// Folds reads of a configured bool field to a constant, in place, across a whole module.
    ///
    /// The trick that makes this safe is that instructions are <b>mutated, never replaced</b>.
    /// Cecil's <c>ILProcessor.Replace</c> is an insert-then-remove: any branch, switch case or
    /// exception-handler boundary still pointing at the old <see cref="Instruction"/> object is left
    /// dangling, and fixing all of those up by hand is the part of this kind of patch that quietly
    /// goes wrong. Changing <see cref="Instruction.OpCode"/> and <see cref="Instruction.Operand"/> on
    /// the existing object keeps its identity, so every reference to it stays correct for free.
    ///
    /// Sizes only ever shrink - <c>ldsfld</c> (5 bytes) becomes <c>ldc.i4.0</c> (1), and
    /// <c>ldfld</c> (5) becomes <c>pop</c> + <c>ldc.i4.0</c> (2) - so short-form branches that were
    /// in range before are still in range, and stack depth is unchanged, so MaxStack still holds.
    /// </summary>
    internal static class FieldReadRewriter
    {
        internal static RewriteResult Rewrite(ModuleDefinition module, PatcherConfig config, ManualLogSource log)
        {
            var result = new RewriteResult();

            if (config.Fields.Count == 0)
            {
                log.LogWarning("No fields configured; nothing to do.");
                result.SummaryLines.Add("no fields configured");
                return result;
            }

            int readers = 0, folded = 0, skippedAddressOf = 0;
            var perMethod = new List<string>();

            foreach (TypeDefinition type in module.GetTypes())
            {
                foreach (MethodDefinition method in type.Methods)
                {
                    if (!method.HasBody) continue;

                    bool touched = false;
                    bool honest = IsAllowlisted(method, config.Allowlist);
                    ILProcessor il = null;

                    // Snapshot: the loop body mutates the instruction list.
                    Instruction[] instructions = new Instruction[method.Body.Instructions.Count];
                    method.Body.Instructions.CopyTo(instructions, 0);

                    foreach (Instruction ins in instructions)
                    {
                        var field = ins.Operand as FieldReference;
                        if (field == null) continue;
                        if (!config.Fields.Contains(Key(field))) continue;

                        readers++;

                        if (ins.OpCode == OpCodes.Ldsfld)
                        {
                            ins.OpCode = honest ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
                            ins.Operand = null;
                            folded++;
                            touched = true;
                        }
                        else if (ins.OpCode == OpCodes.Ldfld)
                        {
                            // The instance reference is already on the stack. Dropping the load
                            // outright would leave it there and produce unverifiable IL, so the
                            // read becomes `pop; ldc.i4.<n>`. Anything branching to this offset
                            // still lands on the pop, which is the correct entry point.
                            il = il ?? method.Body.GetILProcessor();
                            ins.OpCode = OpCodes.Pop;
                            ins.Operand = null;
                            il.InsertAfter(ins, il.Create(honest ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
                            folded++;
                            touched = true;
                        }
                        else if (ins.OpCode == OpCodes.Ldsflda || ins.OpCode == OpCodes.Ldflda)
                        {
                            // An address-of cannot be folded to a constant - the callee may write
                            // through the pointer, and a constant has no address. This is not
                            // hypothetical: `Game.isModded.ToString()` compiles to `ldsflda`,
                            // because calling an instance method on a value type needs a managed
                            // pointer. That one is the startup log line, which is allowlisted
                            // anyway, so it reads the field's real value and stays honest.
                            skippedAddressOf++;
                            log.LogInfo($"  skipped {Describe(method)} - {ins.OpCode} (address-of cannot be folded)");
                        }
                        else
                        {
                            // stsfld/stfld and anything else: we only ever change what readers see,
                            // never what writers do.
                            log.LogDebug($"  ignored {Describe(method)} - {ins.OpCode}");
                        }
                    }

                    if (touched)
                    {
                        perMethod.Add($"  {Describe(method)} -> {(honest ? "true" : "false")}");
                    }
                }
            }

            string targets = string.Join(", ", ToArray(config.Fields));
            log.LogInfo($"{readers} read(s) of [{targets}] across the module; {folded} folded, {skippedAddressOf} skipped.");
            foreach (string line in perMethod) log.LogInfo(line);

            result.SummaryLines.Add($"{readers} read(s) of [{targets}]: {folded} folded, {skippedAddressOf} skipped (address-of)");
            result.SummaryLines.AddRange(perMethod);
            return result;
        }

        private static string Key(FieldReference field)
        {
            return field.DeclaringType.FullName + "::" + field.Name;
        }

        private static string Describe(MethodDefinition method)
        {
            return method.DeclaringType.FullName + "::" + method.Name;
        }

        /// <summary>
        /// Whether this method is one of the places that only reports the flag, and so should keep
        /// seeing <c>true</c>.
        ///
        /// Matches the plain <c>Type::Method</c> form, and also the compiler-generated members a
        /// method spawns - lambdas, iterator and async state machines all end up on a nested type
        /// with a mangled name like <c>FejdStartup/&lt;&gt;c::&lt;SetupGui&gt;b__12_0</c>. Listing
        /// <c>FejdStartup::SetupGui</c> covers those too, which is what someone writing the config
        /// would expect.
        /// </summary>
        private static bool IsAllowlisted(MethodDefinition method, HashSet<string> allowlist)
        {
            if (allowlist.Count == 0) return false;
            if (allowlist.Contains(Describe(method))) return true;

            TypeDefinition declaring = method.DeclaringType;
            if (declaring.DeclaringType == null) return false;

            string outer = declaring.DeclaringType.FullName;
            foreach (string entry in allowlist)
            {
                int sep = entry.IndexOf("::", StringComparison.Ordinal);
                if (sep <= 0) continue;
                if (!string.Equals(entry.Substring(0, sep), outer, StringComparison.Ordinal)) continue;

                string enclosing = entry.Substring(sep + 2);
                if (method.Name.IndexOf("<" + enclosing + ">", StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        private static string[] ToArray(HashSet<string> set)
        {
            var array = new string[set.Count];
            set.CopyTo(array);
            return array;
        }
    }
}
