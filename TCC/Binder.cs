using System;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace TCC
{
	public class Binder
	{
		private CC compiler;

		// Array of types that we can marshal directly, otherwise we use handles
		private Type[] simpleTypes = {
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
		}

		public void BindClass(Type klass)
		{
			var klassName = klass.Name.ToLower();

			// Handle destructor, hands control back to the GC
			compiler.AddSymbol(klassName + "_gc_free", GenerateGCFree(klass));

			// Properties
			var props = klass.GetProperties();
			foreach(var prop in props)
			{
				var propName = prop.Name.ToLower();

				if(prop.GetGetMethod().IsPublic)
					compiler.AddSymbol(klassName + "_get_" + propName, GeneratePropertyGetter(klass, prop));

				if(prop.GetSetMethod().IsPublic)
					compiler.AddSymbol(klassName + "_set_" + propName, GeneratePropertySetter(klass, prop));
			}
		}

		private Delegate GeneratePropertyGetter(Type klass, PropertyInfo property)
		{
			DynamicMethod propertyGetter = new DynamicMethod(
				klass.Name + property.Name + "Getter",
				property.PropertyType,
				new Type[]{ typeof(IntPtr) });

			ILGenerator il = propertyGetter.GetILGenerator();
			il.DeclareLocal(typeof(GCHandle));
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("FromIntPtr"));
			il.Emit(OpCodes.Stloc_0);
			il.Emit(OpCodes.Ldloca_S, 0);
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("get_Target"));
			if(klass.IsValueType)
				il.Emit(OpCodes.Unbox, klass);
			il.Emit(OpCodes.Callvirt, property.GetGetMethod());
			il.Emit(OpCodes.Ret);

			Type getterFunc = typeof(Func<,>);
			Type genericDelegate = getterFunc.MakeGenericType(typeof(IntPtr), property.PropertyType);

			return propertyGetter.CreateDelegate(genericDelegate);
		}

		private Delegate GeneratePropertySetter(Type klass, PropertyInfo property)
		{
			DynamicMethod propertySetter = new DynamicMethod(
				klass.Name + property.Name + "Setter",
				typeof(void),
				new Type[]{ typeof(IntPtr), property.PropertyType });

			ILGenerator il = propertySetter.GetILGenerator();
			il.DeclareLocal(typeof(GCHandle));
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("FromIntPtr"));
			il.Emit(OpCodes.Stloc_0);
			il.Emit(OpCodes.Ldloca_S, 0);
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("get_Target"));
			if(klass.IsValueType)
				il.Emit(OpCodes.Unbox, klass);
			il.Emit(OpCodes.Ldarg_1);
			il.Emit(OpCodes.Callvirt, property.GetSetMethod());
			il.Emit(OpCodes.Ret);

			Type setterFunc = typeof(Action<,>);
			Type genericDelegate = setterFunc.MakeGenericType(typeof(IntPtr), property.PropertyType);

			return propertySetter.CreateDelegate(genericDelegate);
		}

		private Delegate GenerateGCFree(Type klass)
		{
			DynamicMethod klassFree = new DynamicMethod(
				klass.Name + "GCFree",
				typeof(void),
				new Type[]{ typeof(IntPtr) });

			ILGenerator il = klassFree.GetILGenerator();
			il.DeclareLocal(typeof(GCHandle));
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("FromIntPtr"));
			il.Emit(OpCodes.Stloc_0);
			il.Emit(OpCodes.Ldloca_S, 0);
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("Free"));
			il.Emit(OpCodes.Ret);

			Type freeFunc = typeof(Action<>);
			Type genericDelegate = freeFunc.MakeGenericType(typeof(IntPtr));

			return klassFree.CreateDelegate(genericDelegate);
		}
	}
}

