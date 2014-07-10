using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telligent.Evolution.Extensibility.Api.Entities.Version1;
using Telligent.Evolution.Extensibility.Version1;
using System.Collections;

namespace Telligent.BigSocial.Polling.WidgetApi
{
	[Documentation(Category = "Polling")]
	public class PollUrls
	{
		[Documentation("The current poll URL.  The name of the URL method that generates the URL is returned.")]
		public string Current
		{
			get
			{
				var context = System.Web.HttpContext.Current;
				if (context == null)
					return null;

				var url = context.Request.RawUrl;
				if (url.Contains("/p/poll.aspx"))
					return "Poll";
				else if (url.Contains("/p/polls.aspx"))
					return "PollList";
				else if (url.Contains("/p/createeditpoll.aspx"))
				{
					Guid id;
					if (Guid.TryParse(context.Request.QueryString["PollId"], out id))
						return "EditPoll";
					else
						return "CreatePoll";
				}

				return null;
			}
		}

		[Documentation("View an individual poll")]
		public string Poll(
			[Documentation("The poll's identifier.")]
			Guid pollId
			)
		{
			return Poll(pollId, true);
		}

		[Documentation("View an individual poll")]
		public string Poll(
			[Documentation("The poll's identifier.")]
			Guid pollId, 
			[Documentation("True if permissions to view this URL should be validated before returning the URL.")]
			bool checkPermissions
			)
		{
			return InternalApi.PollingService.PollUrl(pollId);
		}

		[Documentation("Edit a poll")]
		public string EditPoll(
			[Documentation("The poll's identifier.")]
			Guid pollId
			)
		{
			return EditPoll(pollId, true);
		}

		[Documentation("Edit a poll")]
		public string EditPoll(
			[Documentation("The poll's identifier.")]
			Guid pollId,
			[Documentation("True if permissions to view this URL should be validated before returning the URL.")]
			bool checkPermissions
			)
		{
			return InternalApi.PollingService.EditPollUrl(pollId, checkPermissions);
		}

		[Documentation("Create a poll")]
		public string CreatePoll(
			[Documentation("The group identifier.")]
			int groupId
			)
		{
			return CreatePoll(groupId, true);
		}

		[Documentation("Create a poll")]
		public string CreatePoll(
			[Documentation("The group identifier.")]
			int groupId,
			[Documentation("True if permissions to view this URL should be validated before returning the URL.")]
			bool checkPermissions
			)
		{
			return InternalApi.PollingService.CreatePollUrl(groupId, checkPermissions);
		}

		[Documentation("List polls in a group")]
		public string PollList(
			[Documentation("The group identifier.")]
			int groupId
			)
		{
			return PollList(groupId, true);
		}

		[Documentation("List polls in a group")]
		public string PollList(
			[Documentation("The group identifier.")]
			int groupId,
			[Documentation("True if permissions to view this URL should be validated before returning the URL.")]
			bool checkPermissions
			)
		{
			return InternalApi.PollingService.PollListUrl(groupId);
		}
	}
}
