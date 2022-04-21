using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Baracuda.Monitoring.Attributes;
using Baracuda.Monitoring.Internal.Profiling;
using Baracuda.Monitoring.Internal.Reflection;
using Baracuda.Monitoring.Internal.Units;
using Baracuda.Monitoring.Internal.Utilities;
using Baracuda.Monitoring.Management;
using Baracuda.Monitoring.Utilities.Pooling.Concretions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Scripting;
using Assembly = System.Reflection.Assembly;

namespace Monitoring.Editor
{
    public class IL2CPPBuildPreprocessor : IPreprocessBuildWithReport
    {
        #region --- Interface & Public Access ---

        /// <summary>
        /// Call this method to manually generate AOT types fort IL2CPP scripting backend.
        /// You can set the filepath of the target script file in the monitoring settings.
        /// </summary>
        public static void GenerateIL2CPPAheadOfTimeTypes()
        {
            OnPreprocessBuildInternal();
        }
        
        public int callbackOrder => MonitoringSettings.Instance().PreprocessBuildCallbackOrder;
        
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!MonitoringSettings.Instance().UseIPreprocessBuildWithReport)
            {
                return;
            }
            
            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if(PlayerSettings.GetScriptingBackend(group) == ScriptingImplementation.IL2CPP)
            {
                OnPreprocessBuildInternal();
            }
        }
        
        #endregion

        //--------------------------------------------------------------------------------------------------------------

        #region --- Enum Types ---

        /// <summary>
        /// Concrete 8 bit enum type definition used for IL2CPP AOT compilation.
        /// </summary>
        public enum Enum8 : byte
        {
        }
    
        /// <summary>
        /// Concrete 16 bit enum type definition used for IL2CPP AOT compilation.
        /// </summary>
        public enum Enum16 : short
        {
        }
    
        /// <summary>
        /// Concrete 32 bit enum type definition used for IL2CPP AOT compilation.
        /// </summary>
        public enum Enum32 : int
        {
        }
    
        /// <summary>
        /// Concrete 64 bit enum type definition used for IL2CPP AOT compilation.
        /// </summary>
        public enum Enum64 : long
        {
        }
        
        #endregion
        
        //--------------------------------------------------------------------------------------------------------------

        #region --- Data & Nested Types ---
        
        private const BindingFlags STATIC_FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags INSTANCE_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static List<string> errorLog = null;
        
        private class TypeDefinitionResult
        {
            public readonly string FullDefinition;
            public readonly string RawDefinition;

            public TypeDefinitionResult(string fullDefinition, string rawDefinition)
            {
                FullDefinition = fullDefinition;
                RawDefinition = rawDefinition;
            }
        }
        
        #endregion

        #region --- Preprocess ---

        private static void OnPreprocessBuildInternal()
        {
            Debug.Log("Starting IL2CPP AOT Type Definition Generation");
            errorLog = new List<string>();
            var typeDefinitionResults = GetTypeDefinitions();

            var content = new StringBuilder();

            content.Append('\n');
            content.Append("//---------- ----------------------------- ----------\n");
            content.Append("//---------- !!! DONT CHANGE THIS FILE !!! ----------\n");
            content.Append("//---------- ----------------------------- ----------\n");
            content.Append("//---------- !!! AUTOGENERATED CONTENT !!! ----------\n");
            content.Append("//---------- ----------------------------- ----------\n");
            content.Append('\n');
            content.Append("//---------- ----------------------------- ----------\n");
            content.Append("//---------- !!! IL2CPP AOT COMPILATION !! ----------\n");
            content.Append("//---------- ----------------------------- ----------\n");
            content.Append("//");
            content.Append(DateTime.Now.ToString("u"));
            content.Append('\n');
            content.Append('\n');
            content.Append("internal class IL2CPP_AOT\n{");
            
            for (var index = 0; index < typeDefinitionResults.Length; index++)
            {
                var result = typeDefinitionResults[index];
                content.Append("\n    ");
                content.Append("//");
                content.Append(result.RawDefinition);
                content.Append("\n    ");
                content.Append('[');
                content.Append(typeof(PreserveAttribute).FullName);
                content.Append(']');
                content.Append("\n    ");
                content.Append(result.FullDefinition);
                content.Append(' ');
                content.Append("AOT_GENERATED_TYPE_");
                content.Append(index++);
                content.Append(';');
                content.Append("\n    ");
            }

            content.Append('\n');
            content.Append('}');

            var filePath = MonitoringSettings.Instance().FilePathIL2CPPTypes;
            var throwOnError = MonitoringSettings.Instance().ThrowOnTypeGenerationError;

            if (string.IsNullOrWhiteSpace(filePath) || !filePath.EndsWith(".cs"))
            {
                filePath = Path.Combine(Application.dataPath, "IL2CPP_AOT.cs");
            }

            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }    
            
            var stream = new FileStream(filePath, FileMode.OpenOrCreate);
            stream.Dispose();
            File.WriteAllText(filePath, content.ToString());
            
            Debug.Log(filePath);
            
            foreach (var error in errorLog)
            {
                Debug.LogError(error);
            }

            if (throwOnError && errorLog.Any())
            {
                throw new OperationCanceledException("[MONITORING] Exception: Not all AOT types could be generated! " +
                                                     "This may lead to [ExecutionEngineException] exceptions in IL2CPP runtime! " +
                                                     "Cancelling build process!");
            }
            
            Debug.Log("Successfully Completed IL2CPP AOT Type Definition Generation");
        }

        private static void BufferErrorInternal(string error)
        {
            if (errorLog.Contains(error))
            {
                return;
            }
            errorLog.Add(error);
        }
        
        #endregion

        #region --- Profiling ---

        private static TypeDefinitionResult[] GetTypeDefinitions()
        {
            var definitionList = new List<TypeDefinitionResult>(200);
            
            foreach (var filteredAssembly in AssemblyManagement.GetFilteredAssemblies())
            {
                if (IsEditorAssembly(filteredAssembly))
                {
                    continue;
                }

                foreach (var type in filteredAssembly.GetTypes())
                {
                    foreach (var memberInfo in type.GetMembers(STATIC_FLAGS))
                    {
                        if (memberInfo.HasAttribute<MonitorAttribute>(true))
                        {
                            foreach (var value in GetTypeDefinition(memberInfo))
                            {
                                if (value == null)
                                {
                                    continue;
                                }
                                if (!definitionList.Contains(value))
                                {
                                    definitionList.Add(value);
                                }
                            }
                        }
                    }
                    
                    foreach (var memberInfo in type.GetMembers(INSTANCE_FLAGS))
                    {
                        if (memberInfo.HasAttribute<MonitorAttribute>(true))
                        {
                            foreach (var value in GetTypeDefinition(memberInfo))
                            {
                                if (value == null)
                                {
                                    continue;
                                }
                                if (!definitionList.Contains(value))
                                {
                                    definitionList.Add(value);
                                }
                            }
                        }
                    }
                }
            }

            return definitionList.ToArray();
        }


        
        #endregion

        #region --- MemberInfo Profiling ---

          private static IEnumerable<TypeDefinitionResult> GetTypeDefinition(MemberInfo memberInfo) =>
            memberInfo switch
            {
                FieldInfo fieldInfo => GetDefinitionFromFieldInfo(fieldInfo),
                PropertyInfo propertyInfo => GetDefinitionFromPropertyInfo(propertyInfo),
                EventInfo eventInfo => GetDefinitionFromEventInfo(eventInfo),
                _ => null
            };

        private static IEnumerable<TypeDefinitionResult> GetDefinitionFromEventInfo(EventInfo eventInfo)
        {
            var targetType = eventInfo.DeclaringType!;
            var valueType = eventInfo.EventHandlerType!;
            
            yield return CreateTypeDefinitionFor(typeof(EventProfile<,>), targetType, valueType);
            yield return CreateTypeDefinitionFor(typeof(ValueProfile<,>), targetType, valueType);
            yield return CreateTypeDefinitionFor(typeof(EventUnit<,>), targetType, valueType);
            yield return CreateTypeDefinitionFor(typeof(ValueUnit<,>), targetType, valueType);
        }
        
        private static IEnumerable<TypeDefinitionResult> GetDefinitionFromPropertyInfo(PropertyInfo propertyInfo)
        {
            var targetType = propertyInfo.DeclaringType!;
            var valueType = propertyInfo.PropertyType!;
            
            yield return CreateTypeDefinitionFor(typeof(PropertyProfile<,>), targetType, valueType);
            yield return CreateTypeDefinitionFor(typeof(ValueProfile<,>), targetType, valueType);
            yield return CreateTypeDefinitionFor(typeof(PropertyUnit<,>), targetType, valueType);
            yield return CreateTypeDefinitionFor(typeof(ValueUnit<,>), targetType, valueType);
        }
        
        private static IEnumerable<TypeDefinitionResult> GetDefinitionFromFieldInfo(FieldInfo fieldInfo)
        {
            var targetType = fieldInfo.DeclaringType!;
            var valueType = fieldInfo.FieldType!;

            yield return CreateTypeDefinitionFor(typeof(FieldProfile<,>), targetType, valueType);
            yield return CreateTypeDefinitionFor(typeof(ValueProfile<,>), targetType, valueType);
            yield return CreateTypeDefinitionFor(typeof(FieldUnit<,>), targetType, valueType);
            yield return CreateTypeDefinitionFor(typeof(ValueUnit<,>), targetType, valueType);
        }

        
        #endregion

        //--------------------------------------------------------------------------------------------------------------

        #region --- Create TypeDefinitionResult ---

        private static TypeDefinitionResult CreateTypeDefinitionFor(Type generic, Type targetType, Type valueType)
        {
            CheckType(generic, out var parsedGenericType);
            CheckType(targetType, out var parsedTargetType);
            CheckType(valueType, out var parsedValueType);

            if (parsedGenericType == null || parsedTargetType == null || parsedValueType == null)
            {
                return null;
            }
            
            var typedGeneric = parsedGenericType.MakeGenericType(parsedTargetType, parsedValueType);

            var fullDefinition = ToGenericTypeStringFullName(typedGeneric);
            var rawDefinition = generic.MakeGenericType(targetType, valueType).ToSyntaxString();
            
            return new TypeDefinitionResult(fullDefinition, rawDefinition);
        }
        
        #endregion

        #region --- Helper ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckType(Type type, out Type validated)
        {
            if (type == typeof(object))
            {
                validated = type;
                return;
            }
            
            if (type.IsAccessible())
            {
                validated = type;
                return;
            }

            if (type.IsClass)
            {
                validated = typeof(object);
                return;
            }

            if (type.IsEnum)
            {
                switch (Marshal.SizeOf(Enum.GetUnderlyingType(type)))
                {
                    case 1:
                        validated = typeof(Enum8);
                        return;
                    case 2:
                        validated = typeof(Enum16);
                        return;
                    case 4:
                        validated = typeof(Enum32);  
                        return;
                    case 8:
                        validated = typeof(Enum64);
                        return;
                }
            }
            
            var error = $"[MONITORING] Error: {type.ToSyntaxString()} is not accessible! ({type.FullName!.Replace('+', '.')})" +
                    $"\nCannot generate AOT code for unmanaged internal/private types! " +
                    $"Please make sure that {type.ToSyntaxString()} and all of its declaring types are either public or use a managed type instead of struct!";
            
            BufferErrorInternal(error);
            validated = null;
        }
        
                
        private static UnityEditor.Compilation.Assembly[] UnityAssemblies { get; }

        static IL2CPPBuildPreprocessor()
        {
            UnityAssemblies = CompilationPipeline.GetAssemblies();
        }

        public static bool IsEditorAssembly(Assembly assembly)
        {
            var editorAssemblies = UnityAssemblies;

            for (var i = 0; i < editorAssemblies.Length; i++)
            {
                var unityAssembly = editorAssemblies[i];

                if (unityAssembly.name != assembly.GetName().Name)
                {
                    continue;
                }

                if (unityAssembly.flags.HasFlagUnsafe(AssemblyFlags.EditorAssembly))
                {
                    return true;
                }
            }

            return false;
        }


        //--------------------------------------------------------------------------------------------------------------
        
        private static readonly Dictionary<Type, string> typeCacheFullName = new Dictionary<Type, string>();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToGenericTypeStringFullName(Type type)
        {
            if (typeCacheFullName.TryGetValue(type, out var value))
            {
                return value;
            }
            
            if (type.IsStatic())
            {
                return typeof(System.Object).FullName!.Replace('+', '.');
            }

            if (type.IsGenericType)
            {
                using var builder = ConcurrentStringBuilderPool.GetDisposable();
                using var argBuilder = ConcurrentStringBuilderPool.GetDisposable();

                var arguments = type.GetGenericArguments();

                foreach (var t in arguments)
                {
                    // Let's make sure we get the argument list.
                    var arg = ToGenericTypeStringFullName(t);

                    if (argBuilder.Value.Length > 0)
                    {
                        argBuilder.Value.AppendFormat(", {0}", arg);
                    }
                    else
                    {
                        argBuilder.Append(arg);
                    }
                }

                if (argBuilder.Value.Length > 0)
                {
                    builder.Value.AppendFormat("{0}<{1}>", type.FullName!.Split('`')[0],
                        argBuilder.ToString());
                }

                var retType = builder.ToString();

                typeCacheFullName.Add(type, retType);
                return retType.Replace('+', '.');
            }

            typeCacheFullName.Add(type, type.FullName);
            
            
            return type.FullName!.Replace('+', '.');
        }
        
        #endregion
    }
}
