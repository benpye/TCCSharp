using System;

namespace TCC
{
	/// <summary>
	/// Used to override the symbol conversion defined in the Binder properties.
	/// </summary>
	public class CSymbolAttribute : Attribute
	{
		public CSymbolAttribute(string nameOverride)
		{
			this.nameOverride = nameOverride;
		}

		private string nameOverride;

		public string NameOverride
		{
			get { return nameOverride; }
		}
	}
}

