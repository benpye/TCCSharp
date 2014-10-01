using System;

namespace TCC
{
	public class NameAttribute : Attribute
	{
		public NameAttribute(string nameOverride)
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

