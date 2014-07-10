using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telligent.Evolution.Extensibility.Api.Version1;
using TEApi = Telligent.Evolution.Extensibility.Api.Version1.PublicApi;

namespace Telligent.BigSocial.Polling.RestApi
{
	public class Group
	{
		public Group()
		{
		}

		internal Group(int groupId)
		{
			var group = TEApi.Groups.Get(new GroupsGetOptions { Id = groupId });
			if (group != null && !group.HasErrors())
			{
				Name = group.Name;
				Url = TEApi.Url.Absolute(group.Url);
				Id = group.Id.Value;
				AvatarUrl = TEApi.Url.Absolute(group.AvatarUrl);
			}
		}

		public string Name { get; set; }
		public string Url { get; set; }
		public string AvatarUrl { get; set; }
		public int Id { get; set; }
	}
}
