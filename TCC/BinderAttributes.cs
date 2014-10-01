using System;

namespace TCC
{
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

