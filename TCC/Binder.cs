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
		private Type[] simpleTypes = {
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
			compiler.AddSymbolNative("gc_free", GenerateGCFree());
		}

		public void BindClass(Type klass)
		{
			var klassName = klass.Name.ToLower();

			// Properties
			var props = klass.GetProperties();
			foreach (var prop in props)
			{
				var propName = prop.Name.ToLower();

				if (prop.GetGetMethod().IsPublic)
					compiler.AddSymbolNative(klassName + "_get_" + propName, GeneratePropertyGetter(klass, prop));

				if (prop.GetSetMethod().IsPublic)
					compiler.AddSymbolNative(klassName + "_set_" + propName, GeneratePropertySetter(klass, prop));
			}

			// Fields
			var fields = klass.GetFields();
			foreach (var field in fields)
			{
				var fieldName = field.Name.ToLower();

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
				var methodName = method.Name.ToLower();

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

				if (constructor.IsPublic)
				{
					var symbolName = klassName + "_new";
					if (constructorParameters.Length > 0)
						symbolName += "_" + constructorName;
					compiler.AddSymbolNative(symbolName, GenerateConstructor(klass, constructor));
				}
			}
		}

		public Delegate GenerateConstructor(Type klass, ConstructorInfo method)
		{
			bool isStatic = method.IsStatic;
			bool isMarshallableReturn = IsMarshallableType(klass);

			var parameterInfos = method.GetParameters();
			List<Type> parameterTypesList = new List<Type>();
			List<Tuple<Type, bool>> marshalTypes = new List<Tuple<Type, bool>>();

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

			Type returnType = isMarshallableReturn ? klass : typeof(IntPtr);

			var parameterTypes = parameterTypesList.ToArray();

			DynamicMethod methodMethod = new DynamicMethod(
				"TCC" + klass.Name + method.Name + "Method",
				returnType,
				parameterTypes,
				true);

			ILGenerator il = methodMethod.GetILGenerator();

			int argc = 0;

			foreach (var t in marshalTypes)
			{
				il.Emit(OpCodes.Ldarg, argc);
				argc++;
				if (t.Item2)
				{
					il.GetClass(t.Item1);
				}
			}

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

			var parameterInfos = method.GetParameters();
			List<Type> parameterTypesList = new List<Type>();
			List<Tuple<Type, bool>> marshalTypes = new List<Tuple<Type, bool>>();

			if (!isStatic)
				parameterTypesList.Add(typeof(IntPtr));

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

			Type returnType = isMarshallableReturn ? method.ReturnType : typeof(IntPtr);

			var parameterTypes = parameterTypesList.ToArray();

			DynamicMethod methodMethod = new DynamicMethod(
				"TCC" + klass.Name + method.Name + "Method",
				returnType,
				parameterTypes,
				true);

			ILGenerator il = methodMethod.GetILGenerator();

			int argc = 0;

			if (!isStatic)
			{
				il.Emit(OpCodes.Ldarg_0);
				il.GetClass(klass);
				argc++;
			}

			foreach (var t in marshalTypes)
			{
				il.Emit(OpCodes.Ldarg, argc);
				argc++;
				if (t.Item2)
				{
					il.GetClass(t.Item1);
				}
			}

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

		public Delegate GeneratePropertyGetter(Type klass, PropertyInfo property)
		{
			bool isStatic = property.GetGetMethod().IsStatic;
			bool isMarshallable = IsMarshallableType(property.PropertyType);

			Type[] parameterTypes = isStatic ? new Type[] { } : new Type[] { typeof(IntPtr) };
			Type returnType = isMarshallable ? property.PropertyType : typeof(IntPtr);

			DynamicMethod propertyGetter = new DynamicMethod(
				"TCC" + klass.Name + property.Name + "PropertyGetter",
				returnType,
				parameterTypes,
				true);

			ILGenerator il = propertyGetter.GetILGenerator();

			if (!isStatic)
			{
				il.Emit(OpCodes.Ldarg_0);
				il.GetClass(klass);
			}

			if (isStatic || klass.IsValueType)
				il.Emit(OpCodes.Call, property.GetGetMethod());
			else
				il.Emit(OpCodes.Callvirt, property.GetGetMethod());

			if (!isMarshallable)
				il.WrapClass(property.PropertyType);

			il.Emit(OpCodes.Ret);

			Type getterFunc = DelegateWrapper.GenerateDelegateType(returnType, parameterTypes);

			return propertyGetter.CreateDelegate(getterFunc);
		}

		private Delegate GeneratePropertySetter(Type klass, PropertyInfo property)
		{
			bool isStatic = property.GetSetMethod().IsStatic;
			bool isMarshallable = IsMarshallableType(property.PropertyType);

			Type inType = isMarshallable ? property.PropertyType : typeof(IntPtr);
			Type[] parameterTypes = isStatic ? new Type[] { inType } : new Type[] { typeof(IntPtr), inType };
			Type returnType = typeof(void);

			DynamicMethod propertySetter = new DynamicMethod(
				"TCC" + klass.Name + property.Name + "PropertySetter",
				returnType,
				parameterTypes,
				true);

			ILGenerator il = propertySetter.GetILGenerator();

			il.Emit(OpCodes.Ldarg_0);
			if (!isStatic)
			{
				il.GetClass(klass);
				il.Emit(OpCodes.Ldarg_1);
			}

			if (!isMarshallable)
				il.GetClass(property.PropertyType);

			if (isStatic || klass.IsValueType)
				il.Emit(OpCodes.Call, property.GetSetMethod());
			else
				il.Emit(OpCodes.Callvirt, property.GetSetMethod());

			il.Emit(OpCodes.Ret);

			Type setterFunc = DelegateWrapper.GenerateDelegateType(returnType, parameterTypes);

			return propertySetter.CreateDelegate(setterFunc);
		}

		private Delegate GenerateGCFree()
		{
			Type[] parameterTypes = new Type[] { typeof(IntPtr) };
			Type returnType = typeof(void);

			DynamicMethod gcFree = new DynamicMethod(
				"TCCGCFree",
				returnType,
				parameterTypes,
				true);

			ILGenerator il = gcFree.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.GetGCHandle();
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("Free"));
			il.Emit(OpCodes.Ret);

			Type freeFunc = DelegateWrapper.GenerateDelegateType(returnType, parameterTypes);

			return gcFree.CreateDelegate(freeFunc);
		}

		private bool IsMarshallableType(Type klass)
		{
			return simpleTypes.Contains(klass.GetElementType()) || simpleTypes.Contains(klass) || klass.IsEnum;
		}
	}
}

