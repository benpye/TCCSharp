using System;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TCC
{
	public class Binder
	{
		private CC compiler;

		// Array of types that we can marshal directly, otherwise we use handles
		private static Type[] simpleTypes = {
			typeof(void),
			typeof(IntPtr),
			typeof(Byte),
			typeof(Int16),
			typeof(UInt16),
			typeof(Int32),
			typeof(UInt32),
			typeof(Char),
			typeof(String),
			typeof(StringBuilder),
			typeof(Single),
			typeof(Double)
		};

		public Binder(CC tcc)
		{
			compiler = tcc;

			// Generic GC free
			compiler.AddSymbol("gc_free", (Action<IntPtr>)GCFree);
		}

		private static string GetNameFromAttributes(string defaultValue, MemberInfo obj)
		{
			foreach (var a in obj.GetCustomAttributes(true))
			{
				if (a is NameAttribute)
				{
					return (a as NameAttribute).NameOverride;
				}
			}

			return defaultValue;
		}

		public void BindClass(Type klass, string prefix = "")
		{
			var klassName = prefix + GetNameFromAttributes(klass.Name.ToLower(), klass);

			// Properties
			var props = klass.GetProperties();
			foreach (var prop in props)
			{
				var propName = GetNameFromAttributes(prop.Name.ToLower(), prop);

				if (prop.GetGetMethod().IsPublic)
					compiler.AddSymbolNative(klassName + "_get_" + propName, GenerateMethod(klass, prop.GetGetMethod()));

				if (prop.GetSetMethod().IsPublic)
					compiler.AddSymbolNative(klassName + "_set_" + propName, GenerateMethod(klass, prop.GetSetMethod()));
			}

			// Fields
			var fields = klass.GetFields();
			foreach (var field in fields)
			{
				var fieldName = GetNameFromAttributes(field.Name.ToLower(), field);

				if (field.IsPublic)
				{
					compiler.AddSymbolNative(klassName + "_get_" + fieldName, GenerateFieldGetter(klass, field));
					compiler.AddSymbolNative(klassName + "_set_" + fieldName, GenerateFieldSetter(klass, field));
				}
			}

			// Methods
			var methods = klass.GetMethods();
			foreach (var method in methods)
			{
				var methodName = GetNameFromAttributes(method.Name.ToLower(), method);

				if (method.IsPublic && !method.IsSpecialName)
				{
					compiler.AddSymbolNative(klassName + "_" + methodName, GenerateMethod(klass, method));
				}
			}

			// Constructors
			var constructors = klass.GetConstructors();
			foreach (var constructor in constructors)
			{
				var constructorParameters = constructor.GetParameters();
				string constructorName = "";

				foreach (var p in constructorParameters)
					constructorName += p.ParameterType.Name.ToLower();

				var symbolName = "new";
				if (constructorParameters.Length > 0)
					symbolName += "_" + constructorName;

				symbolName = GetNameFromAttributes(symbolName, constructor);

				if (constructor.IsPublic)
				{
					compiler.AddSymbolNative(klassName + "_" + symbolName, GenerateConstructor(klass, constructor));
				}
			}
		}

		private static void GetParameterInfo(ParameterInfo[] parameterInfos, 
			List<Type> parameterTypesList, 
			List<Tuple<Type, bool>> marshalTypes)
		{
			foreach (var p in parameterInfos)
			{
				if (IsMarshallableType(p.ParameterType))
				{
					parameterTypesList.Add(p.ParameterType);
					marshalTypes.Add(new Tuple<Type, bool>(p.ParameterType, false));
				}
				else
				{
					parameterTypesList.Add(typeof(IntPtr));
					marshalTypes.Add(new Tuple<Type, bool>(p.ParameterType, true));
				}
			}
		}

		public Delegate GenerateConstructor(Type klass, ConstructorInfo method)
		{
			bool isMarshallableReturn = IsMarshallableType(klass);

			List<Type> parameterTypesList = new List<Type>();
			List<Tuple<Type, bool>> marshalTypes = new List<Tuple<Type, bool>>();

			GetParameterInfo(method.GetParameters(), parameterTypesList, marshalTypes);

			Type returnType = isMarshallableReturn ? klass : typeof(IntPtr);

			var parameterTypes = parameterTypesList.ToArray();

			DynamicMethod methodMethod = new DynamicMethod(
				"TCC" + klass.Name + method.Name + "Method",
				returnType,
				parameterTypes,
				true);

			ILGenerator il = methodMethod.GetILGenerator();

			il.MarshalMethodArgs(klass, true, marshalTypes);

			il.Emit(OpCodes.Newobj, method);

			if (!isMarshallableReturn)
				il.WrapClass(klass);

			il.Emit(OpCodes.Ret);

			Type methodFunc = DelegateWrapper.GenerateDelegateType(returnType, parameterTypes);

			return methodMethod.CreateDelegate(methodFunc);
		}

		public Delegate GenerateMethod(Type klass, MethodInfo method)
		{
			bool isStatic = method.IsStatic;
			bool isMarshallableReturn = IsMarshallableType(method.ReturnType);

			List<Type> parameterTypesList = new List<Type>();
			List<Tuple<Type, bool>> marshalTypes = new List<Tuple<Type, bool>>();

			if (!isStatic)
				parameterTypesList.Add(typeof(IntPtr));

			GetParameterInfo(method.GetParameters(), parameterTypesList, marshalTypes);

			Type returnType = isMarshallableReturn ? method.ReturnType : typeof(IntPtr);

			var parameterTypes = parameterTypesList.ToArray();

			DynamicMethod methodMethod = new DynamicMethod(
				"TCC" + klass.Name + method.Name + "Method",
				returnType,
				parameterTypes,
				true);

			ILGenerator il = methodMethod.GetILGenerator();

			il.MarshalMethodArgs(klass, isStatic, marshalTypes);

			if (isStatic || klass.IsValueType)
				il.Emit(OpCodes.Call, method);
			else
				il.Emit(OpCodes.Callvirt, method);

			if (!isMarshallableReturn)
				il.WrapClass(method.ReturnType);

			il.Emit(OpCodes.Ret);

			Type methodFunc = DelegateWrapper.GenerateDelegateType(returnType, parameterTypes);

			return methodMethod.CreateDelegate(methodFunc);
		}

		public Delegate GenerateFieldGetter(Type klass, FieldInfo field)
		{
			bool isStatic = field.IsStatic;
			bool isMarshallable = IsMarshallableType(field.FieldType);

			Type[] parameterTypes = isStatic ? new Type[] { } : new Type[] { typeof(IntPtr) };
			Type returnType = isMarshallable ? field.FieldType : typeof(IntPtr);

			DynamicMethod fieldGetter = new DynamicMethod(
				"TCC" + klass.Name + field.Name + "FieldGetter",
				returnType,
				parameterTypes,
				true);

			ILGenerator il = fieldGetter.GetILGenerator();

			if (!isStatic)
			{
				il.Emit(OpCodes.Ldarg_0);
				il.GetClass(klass);
			}

			if (!isStatic)
				il.Emit(OpCodes.Ldfld, field);
			else
				il.Emit(OpCodes.Ldsfld, field);

			if (!isMarshallable)
				il.WrapClass(field.FieldType);

			il.Emit(OpCodes.Ret);

			Type getterFunc = DelegateWrapper.GenerateDelegateType(returnType, parameterTypes);

			return fieldGetter.CreateDelegate(getterFunc);
		}

		public Delegate GenerateFieldSetter(Type klass, FieldInfo field)
		{
			bool isStatic = field.IsStatic;
			bool isMarshallable = IsMarshallableType(field.FieldType);

			Type inType = isMarshallable ? field.FieldType : typeof(IntPtr);
			Type[] parameterTypes = isStatic ? new Type[] { inType } : new Type[] { typeof(IntPtr), inType };
			Type returnType = typeof(void);

			DynamicMethod fieldSetter = new DynamicMethod(
				"TCC" + klass.Name + field.Name + "FieldSetter",
				returnType,
				parameterTypes,
				true);

			ILGenerator il = fieldSetter.GetILGenerator();

			il.Emit(OpCodes.Ldarg_0);
			if (!isStatic)
			{
				il.GetClass(klass);
				il.Emit(OpCodes.Ldarg_1);
			}

			if (!isMarshallable)
				il.GetClass(field.FieldType);

			if (!isStatic)
				il.Emit(OpCodes.Stfld, field);
			else
				il.Emit(OpCodes.Stsfld, field);

			il.Emit(OpCodes.Ret);

			Type setterFunc = DelegateWrapper.GenerateDelegateType(returnType, parameterTypes);

			return fieldSetter.CreateDelegate(setterFunc);
		}

		private static void GCFree(IntPtr ptr)
		{
			GCHandle gch = GCHandle.FromIntPtr(ptr);
			gch.Free();
		}

		private static bool IsMarshallableType(Type klass)
		{
			return simpleTypes.Contains(klass.GetElementType()) || simpleTypes.Contains(klass) || klass.IsEnum;
		}
	}
}

