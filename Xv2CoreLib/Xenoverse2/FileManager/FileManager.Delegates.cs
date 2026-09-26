using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xv2CoreLib.Resource;

namespace Xv2CoreLib
{
    public partial class FileManager
    {
        private static readonly Dictionary<Type, Tuple<FileLoadAttribute, Func<byte[], object>>> FileTypeLoadersFromBytes;
        private static readonly Dictionary<Type, Tuple<FileLoadAttribute, Func<string, Xv2FileIO, bool, object>>> FileTypeLoadersFromFileSystem;
        private static readonly Dictionary<Type, Tuple<FileSaveAttribute, Func<object, byte[]>>> FileTypeSaversToBytes;
        private static readonly Dictionary<Type, Tuple<FileSaveAttribute, Action<object, string>>> FileTypeSaversToDrive;

        static FileManager()
        {
            FileTypeLoadersFromBytes = new Dictionary<Type, Tuple<FileLoadAttribute, Func<byte[], object>>>(32);
            FileTypeSaversToBytes = new Dictionary<Type, Tuple<FileSaveAttribute, Func<object, byte[]>>>(32);
            FileTypeLoadersFromFileSystem = new Dictionary<Type, Tuple<FileLoadAttribute, Func<string, Xv2FileIO, bool, object>>>(4);
            FileTypeSaversToDrive = new Dictionary<Type, Tuple<FileSaveAttribute, Action<object, string>>>(4);
        }

        private static Tuple<FileLoadAttribute, Func<byte[], object>> GetFromBytesLoadDelegate(Type type)
        {
            if (FileTypeLoadersFromBytes.TryGetValue(type, out Tuple<FileLoadAttribute, Func<byte[], object>> parserMethod))
                return parserMethod;

            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.IsDefined(typeof(FileLoadAttribute), inherit: false)).ToArray();

            if (methods.Length >= 1)
            {
                if (IsValidFromBytesLoadMethod(methods[0]))
                {
                    FileLoadAttribute attribute = methods[0].GetCustomAttribute<FileLoadAttribute>();
                    var delegateTuple = new Tuple<FileLoadAttribute, Func<byte[], object>>(attribute, (Func<byte[], object>)methods[0].CreateDelegate(typeof(Func<byte[], object>)));
                    FileTypeLoadersFromBytes.Add(type, delegateTuple);
                    return delegateTuple;
                }
            }

            return null;
        }

        private static Tuple<FileLoadAttribute, Func<string, Xv2FileIO, bool, object>> GetFromFileSystemLoadDelegate(Type type)
        {
            if (FileTypeLoadersFromFileSystem.TryGetValue(type, out Tuple<FileLoadAttribute, Func<string, Xv2FileIO, bool, object>> parserMethod))
                return parserMethod;

            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.IsDefined(typeof(FileLoadAttribute), inherit: false)).ToArray();

            if (methods.Length >= 1)
            {
                if (IsValidFromFileSystemLoadMethod(methods[0]))
                {
                    FileLoadAttribute attribute = methods[0].GetCustomAttribute<FileLoadAttribute>();
                    var delegateTuple = new Tuple<FileLoadAttribute, Func<string, Xv2FileIO, bool, object>>(attribute, (Func<string, Xv2FileIO, bool, object>)methods[0].CreateDelegate(typeof(Func<string, Xv2FileIO, bool, object>)));
                    FileTypeLoadersFromFileSystem.Add(type, delegateTuple);
                    return delegateTuple;
                }
            }

            return null;
        }

        private static Tuple<FileSaveAttribute, Func<object, byte[]>> GetToBytesSaveDelegate(Type type)
        {
            if (FileTypeSaversToBytes.TryGetValue(type, out Tuple<FileSaveAttribute, Func<object, byte[]>> writerMethod))
                return writerMethod;

            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(method => method.IsDefined(typeof(FileSaveAttribute), inherit: true)).ToArray();

            if (methods.Length >= 1)
            {
                if (IsValidToBytesMethod(methods[0]))
                {
                    FileSaveAttribute attribute = methods[0].GetCustomAttribute<FileSaveAttribute>();
                    var _delegate = new Tuple<FileSaveAttribute, Func<object, byte[]>>(attribute, CreateDelegate<byte[]>(methods[0]));
                    FileTypeSaversToBytes.Add(type, _delegate);
                    return _delegate;
                }
            }

            return null;
        }

        private static Tuple<FileSaveAttribute, Action<object, string>> GetToDriveSaveDelegate(Type type)
        {
            if (FileTypeSaversToDrive.TryGetValue(type, out Tuple<FileSaveAttribute, Action<object, string>> writerMethod))
                return writerMethod;

            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(method => method.IsDefined(typeof(FileSaveAttribute), inherit: true)).ToArray();

            if (methods.Length >= 1)
            {
                if (IsValidToDriveMethod(methods[0]))
                {
                    FileSaveAttribute attribute = methods[0].GetCustomAttribute<FileSaveAttribute>();
                    var _delegate = new Tuple<FileSaveAttribute, Action<object, string>>(attribute, CreateActionDelegate<string>(methods[0]));
                    FileTypeSaversToDrive.Add(type, _delegate);
                    return _delegate;
                }
            }

            return null;
        }

        private static Func<object, Input> CreateDelegate<Input>(MethodInfo method)
        {
            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            MethodCallExpression call = Expression.Call(Expression.Convert(instance, method.DeclaringType!), method);
            Expression<Func<object, Input>> lambda = Expression.Lambda<Func<object, Input>>(call, instance);

            return lambda.Compile();
        }

        private static Action<object, Input> CreateActionDelegate<Input>(MethodInfo method)
        {
            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            MethodCallExpression call = Expression.Call(Expression.Convert(instance, method.DeclaringType!), method);
            Expression<Action<object, Input>> lambda = Expression.Lambda<Action<object, Input>>(call, instance);

            return lambda.Compile();
        }

        private static bool IsValidFromBytesLoadMethod(MethodInfo method)
        {
            if (!method.IsStatic)
                return false;

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(byte[]) || method.ReturnType != method.DeclaringType)
            {
                return false;
            }

            return true;
        }

        private static bool IsValidToBytesMethod(MethodInfo method)
        {
            if (method.IsStatic)
                return false;

            if (method.GetParameters().Length != 0 || method.ReturnType != typeof(byte[]))
            {
                return false;
            }

            return true;
        }

        private static bool IsValidFromFileSystemLoadMethod(MethodInfo method)
        {
            if (!method.IsStatic)
                return false;

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length != 3 ||
                parameters[0].ParameterType != typeof(string) || parameters[1].ParameterType != typeof(Xv2FileIO) ||
                parameters[2].ParameterType != typeof(bool) || method.ReturnType != method.DeclaringType)
            {
                return false;
            }

            return true;
        }

        private static bool IsValidToDriveMethod(MethodInfo method)
        {
            if (method.IsStatic)
                return false;

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string) || method.ReturnType != typeof(void))
            {
                return false;
            }

            return true;
        }

    }
}
