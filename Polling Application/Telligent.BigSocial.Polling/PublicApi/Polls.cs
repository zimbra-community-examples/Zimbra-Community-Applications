using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telligent.Evolution.Extensibility.Api.Entities.Version1;
using Internal = Telligent.BigSocial.Polling.InternalApi;
using Telligent.Evolution.Extensibility.Api.Version1;
using TEApi = Telligent.Evolution.Extensibility.Api.Version1.PublicApi;

namespace Telligent.BigSocial.Polling.PublicApi
{
	public static class Polls
	{
		private static readonly Guid _contentTypeId = new Guid("ddd3324269364ec684846a80c18160d7");
		private static readonly PollEvents _events = new PollEvents();

		public static Guid ContentTypeId { get { return _contentTypeId; } }

		public static PollEvents Events
		{
			get { return _events; }
		}

		public static Poll Get(Guid id)
		{
			try
			{
				var poll = Internal.PollingService.GetPoll(id);
				if (poll == null)
					return null;

				return new Poll(poll);
			}
			catch (Exception ex)
			{
				return new Poll(new AdditionalInfo(new Error(ex.GetType().FullName, ex.Message)));
			}
		}

		public static AdditionalInfo Delete(Guid id)
		{
			try
			{
				var poll = Internal.PollingService.GetPoll(id);
				if (poll != null)
					Internal.PollingService.DeletePoll(poll);

				return new AdditionalInfo();
			}
			catch (Exception ex)
			{
				return new AdditionalInfo(new Error(ex.GetType().FullName, ex.Message));
			}
		}

		public static Poll Create(int groupId, string name, string description = null, DateTime? votingEndDate = null, bool? hideResultsUntilVotingComplete = false)
		{
			try
			{
				var poll = new Internal.Poll();
				poll.GroupId = groupId;
				poll.AuthorUserId = TEApi.Users.AccessingUser.Id.Value;
				poll.Name = name;
				poll.Description = description;
				poll.IsEnabled = true;
				poll.VotingEndDateUtc = votingEndDate;
				poll.HideResultsUntilVotingComplete = hideResultsUntilVotingComplete.HasValue && hideResultsUntilVotingComplete.Value;

				Internal.PollingService.AddUpdatePoll(poll);

				return Get(poll.Id);
			}
			catch (Exception ex)
			{
				return new Poll(new AdditionalInfo(new Error(ex.GetType().FullName, ex.Message)));
			}
		}

		public static Poll Update(Guid id, string name = null, string description = null, DateTime? votingEndDate = null, bool? hideResultsUntilVotingComplete = null, bool? clearVotingEndDate = false)
		{
			try
			{
				var poll = Internal.PollingService.GetPoll(id);
				if (poll != null)
				{
					if (name != null)
						poll.Name = name;

					if (description != null)
						poll.Description = description;

					if (votingEndDate != null)
						poll.VotingEndDateUtc = (DateTime?) Internal.Formatting.FromUserTimeToUtc(votingEndDate.Value);
					else if (clearVotingEndDate.HasValue && clearVotingEndDate.Value)
						poll.VotingEndDateUtc = null;

					if (hideResultsUntilVotingComplete.HasValue)
						poll.HideResultsUntilVotingComplete = hideResultsUntilVotingComplete.Value;

					Internal.PollingService.AddUpdatePoll(poll);
				}

				return Get(id);
			}
			catch (Exception ex)
			{
				return new Poll(new AdditionalInfo(new Error(ex.GetType().FullName, ex.Message)));
			}
		}

		public static PagedList<Poll> List(int groupId, int pageIndex = 0, int pageSize = 20, string sortBy = "Date")
		{
			if (pageSize > 100)
				pageSize = 100;
			else if (pageSize < 1)
				pageSize = 1;

			if (pageIndex < 0)
				pageIndex = 0;

			try
			{
				if (string.Equals(sortBy, "TopPollsScore", StringComparison.OrdinalIgnoreCase))
				{
					var group = TEApi.Groups.Get(new GroupsGetOptions { Id = groupId });
					if (group == null || group.HasErrors())
						return new PagedList<Poll>();

					var scores = TEApi.CalculatedScores.List(Plugins.TopPollsScore.ScoreId, new CalculatedScoreListOptions { ApplicationId = group.ApplicationId, ContentTypeId = ContentTypeId, PageIndex = pageIndex, PageSize = pageSize, SortOrder = "Descending" });

					var polls = new List<Poll>();
					foreach (var score in scores)
					{
						if (score.Content != null)
						{
							var poll = Get(score.Content.ContentId);
							if (poll != null)
								polls.Add(poll);
						}
					}

					return new PagedList<Poll>(polls, scores.PageSize, scores.PageIndex, scores.TotalCount);
				}
				else
				{
					var polls = InternalApi.PollingService.ListPolls(groupId, pageSize, pageIndex);
					return new PagedList<Poll>(polls.Select(x => new Poll(x)), polls.PageSize, polls.PageIndex, polls.TotalCount);
				}				
			}
			catch (Exception ex)
			{
				return new PagedList<Poll>(new AdditionalInfo(new Error(ex.GetType().FullName, ex.Message)));
			}
		}

		public static bool CanCreate(int groupId)
		{
			return InternalApi.PollingService.CanVote(groupId);
		}

		public static bool CanVote(Guid pollId)
		{
			return InternalApi.PollingService.CanVote(pollId, TEApi.Users.AccessingUser.Id.Value);
		}

		public static bool CanEdit(Guid pollId)
		{
			return InternalApi.PollingService.CanModerate(pollId, TEApi.Users.AccessingUser.Id.Value);
		}

		public static bool CanDelete(Guid pollId)
		{
			return InternalApi.PollingService.CanModerate(pollId, TEApi.Users.AccessingUser.Id.Value);
		}

		public static string UI(Guid pollId, bool readOnly = false, bool showNameAndDescription = true)
		{
			return string.Concat(
				"<div class=\"ui-poll\" data-pollid=\"",
				pollId.ToString(),
				"\" data-readonly=\"",
				(readOnly || !CanVote(pollId)).ToString().ToLower(),
				"\" data-showname=\"",
				showNameAndDescription.ToString().ToLower(),
				"\"></div>");
		}
	}
}
