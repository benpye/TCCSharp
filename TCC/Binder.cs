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
				if (a is CSymbolAttribute)
				{
					return (a as CSymbolAttribute).NameOverride;
				}
			}

			return defaultValue;
		}

		private string _constructorPattern = "{class:L}_new{args:L}";
		public string ConstructorPattern
		{
			get { return _constructorPattern; }
			set { _constructorPattern = value; }
		}

		private string _propertyPattern = "{class:L}_{mutator:L}_{property:L}";
		public string PropertyPattern
		{
			get { return _propertyPattern; }
			set { _propertyPattern = value; }
		}


		private string _fieldPattern = "{class:L}_{mutator:L}_{field:L}";
		public string FieldPattern
		{
			get { return _fieldPattern; }
			set { _fieldPattern = value; }
		}


		private string _methodPattern = "{class:L}_{method:L}";
		public string MethodPattern
		{
			get { return _methodPattern; }
			set { _methodPattern = value; }
		}

		public void BindClass(Type klass)
		{
			var klassName = GetNameFromAttributes(klass.Name, klass);

			Dictionary<string, string> formatDictionary = new Dictionary<string, string>();
			formatDictionary["class"] = klassName;

			// Properties
			var props = klass.GetProperties();
			foreach (var prop in props)
			{
				var pattern = GetNameFromAttributes(PropertyPattern, prop);

				formatDictionary["property"] = prop.Name;
				formatDictionary["mutator"]  = "get";

				if (prop.GetGetMethod().IsPublic)
					compiler.AddSymbolNative(pattern.Inject(formatDictionary), GenerateMethod(klass, prop.GetGetMethod()));

				formatDictionary["mutator"]  = "set";

				if (prop.GetSetMethod().IsPublic)
					compiler.AddSymbolNative(pattern.Inject(formatDictionary), GenerateMethod(klass, prop.GetSetMethod()));
			}

			// Fields
			var fields = klass.GetFields();
			foreach (var field in fields)
			{
				var pattern = GetNameFromAttributes(FieldPattern, field);

				formatDictionary["field"] = field.Name;

				if (field.IsPublic)
				{
					formatDictionary["mutator"]  = "get";
					compiler.AddSymbolNative(pattern.Inject(formatDictionary), GenerateFieldGetter(klass, field));
					formatDictionary["mutator"]  = "set";
					compiler.AddSymbolNative(pattern.Inject(formatDictionary), GenerateFieldSetter(klass, field));
				}
			}

			// Methods
			var methods = klass.GetMethods();
			foreach (var method in methods)
			{
				var pattern = GetNameFromAttributes(MethodPattern, method);

				formatDictionary["method"] = method.Name;

				if (method.IsPublic && !method.IsSpecialName)
				{
					compiler.AddSymbolNative(pattern.Inject(formatDictionary), GenerateMethod(klass, method));
				}
			}

			// Constructors
			var constructors = klass.GetConstructors();
			foreach (var constructor in constructors)
			{
				var pattern = GetNameFromAttributes(ConstructorPattern, constructor);

				var constructorParameters = constructor.GetParameters();
				string constructorArgs = "";

				foreach (var p in constructorParameters)
					constructorArgs += p.ParameterType.Name;

				formatDictionary["args"] = constructorArgs;

				if (constructor.IsPublic)
				{
					compiler.AddSymbolNative(pattern.Inject(formatDictionary), GenerateConstructor(klass, constructor));
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

