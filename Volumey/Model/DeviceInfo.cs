using System;

namespace Volumey.Model
{
	[Serializable]
	public class DeviceInfo
	{
		private string _id;
		public string ID
		{
			get => _id;
			private set => _id = value;
		}

		private string _name;
		public string Name
		{
			get => _name;
			set => _name = value;
		}

		public DeviceInfo(string id, string name)
		{
			ID = id;
			Name = name;
		}
	}
}