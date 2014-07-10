using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telligent.Evolution.Extensibility.Caching.Version1;
using Telligent.Evolution.Extensibility.Api.Version1;
using Telligent.Evolution.Extensibility.Api.Entities.Version1;
using TEApi = Telligent.Evolution.Extensibility.Api.Version1.PublicApi;

namespace Telligent.BigSocial.Polling.InternalApi
{
	internal static class PollingService
	{
		internal static void AddUpdatePoll(Poll poll)
		{
			bool isCreate = poll.Id == Guid.Empty;

			ValidatePoll(poll);

			if (isCreate)
				PublicApi.Polls.Events.OnBeforeCreate(poll);
			else
				PublicApi.Polls.Events.OnBeforeUpdate(poll);

			PollingDataService.AddUpdatePoll(poll);
			ExpirePolls(poll.GroupId);

			if (isCreate)
				PublicApi.Polls.Events.OnAfterCreate(poll);
			else
				PublicApi.Polls.Events.OnAfterUpdate(poll);
		}

		internal static void DeletePoll(Poll poll)
		{
			ValidatePoll(poll);

			PublicApi.Polls.Events.OnBeforeDelete(poll);
			PollingDataService.DeletePoll(poll.Id);
			ExpirePolls(poll.GroupId);
			PublicApi.Polls.Events.OnAfterDelete(poll);
		}

		internal static void ReassignPolls(int oldUserId, int newUserId)
		{
			if (oldUserId != newUserId)
			{
				PollingDataService.ReassigneUser(oldUserId, newUserId);
				// We don't expire the cache here because it would be potentially too large of an expiration
			}
		}

		internal static void DeletePolls(int groupId)
		{
			PollingDataService.DeletePollsByGroup(groupId);
			ExpirePolls(groupId);
		}

		internal static Poll GetPoll(Guid pollId)
		{
			Poll poll = (Poll) CacheService.Get(PollCacheKey(pollId), CacheScope.All);
			if (poll == null)
			{
				poll = PollingDataService.GetPoll(pollId);
				if (poll != null)
					CacheService.Put(PollCacheKey(pollId), poll, CacheScope.All, new string[] { PollTag(poll.GroupId) });
			}

			if (poll != null && CanRead(poll.GroupId))
				return poll;

			return null;
		}

		internal static PagedList<Poll> ListPolls(int groupId, int pageSize, int pageIndex)
		{
			if (!CanRead(groupId))
				return new PagedList<Poll>();

			PagedList<Poll> polls = (PagedList<Poll>)CacheService.Get(PollsCacheKey(groupId, pageSize, pageIndex), CacheScope.Context | CacheScope.Process);
			if (polls == null)
			{
				polls = PollingDataService.ListPolls(groupId, pageSize, pageIndex);
				CacheService.Put(PollsCacheKey(groupId, pageSize, pageIndex), polls, CacheScope.Context | CacheScope.Process, new string[] { PollTag(groupId) });
			}

			return polls;
		}

		internal static PagedList<Poll> ListPollsToReindex(int pageSize, int pageIndex)
		{
			return PollingDataService.ListPollsToReindex(pageSize, pageIndex);
		}

		internal static void SetPollsAsIndexed(IEnumerable<Guid> pollIds)
		{
			PollingDataService.SetPollsAsIndexed(pollIds);
		}

		internal static void AddUpdatePollAnswer(PollAnswer answer)
		{
			bool isCreate = answer.Id == Guid.Empty;

			ValidatePollAnswer(answer);

			if (isCreate)
				PublicApi.PollAnswers.Events.OnBeforeCreate(answer);
			else
				PublicApi.PollAnswers.Events.OnBeforeUpdate(answer);

			PollingDataService.AddUpdatePollAnswer(answer);
			ExpirePoll(answer.PollId);

			Poll poll = GetPoll(answer.PollId);
			if (poll != null)
				ExpirePolls(poll.GroupId);

			if (isCreate)
				PublicApi.PollAnswers.Events.OnAfterCreate(answer);
			else
				PublicApi.PollAnswers.Events.OnAfterUpdate(answer);
		}

		internal static void DeletePollAnswer(PollAnswer answer)
		{
			ValidatePollAnswer(answer);
			PublicApi.PollAnswers.Events.OnBeforeDelete(answer);
			PollingDataService.DeletePollAnswer(answer.Id);
			ExpirePoll(answer.PollId);
			
			Poll poll = GetPoll(answer.PollId);
			if (poll != null)
				ExpirePolls(poll.GroupId);

			PublicApi.PollAnswers.Events.OnAfterDelete(answer);
		}

		internal static PollAnswer GetPollAnswer(Guid pollAnswerId)
		{
			var pollAnswer = PollingDataService.GetPollAnswer(pollAnswerId);
			if (pollAnswer != null && GetPoll(pollAnswer.PollId) != null)
				return pollAnswer;

			return null;
		}

		internal static void AddUpdatePollVote(PollVote vote)
		{
			ValidatePollVote(vote);

			var existingVote = GetPollVote(vote.PollId, vote.UserId);
			if (existingVote != null && existingVote.PollAnswerId == vote.PollAnswerId)
				return;

			bool isCreate = existingVote == null;
			if (isCreate)
				PublicApi.PollVotes.Events.OnBeforeCreate(vote);
			else
				PublicApi.PollVotes.Events.OnBeforeUpdate(vote);

			PollingDataService.AddUpdatePollVote(vote);
			ExpirePoll(vote.PollId);

			Poll poll = GetPoll(vote.PollId);
			if (poll != null)
				ExpirePolls(poll.GroupId);

			if (isCreate)
				PublicApi.PollVotes.Events.OnAfterCreate(vote);
			else
				PublicApi.PollVotes.Events.OnAfterUpdate(vote);
		}

		internal static void DeletePollVote(PollVote vote)
		{
			ValidatePollVote(vote);
			PublicApi.PollVotes.Events.OnBeforeDelete(vote);
			PollingDataService.DeletePollVote(vote.PollId, vote.UserId);
			ExpirePoll(vote.PollId);

			Poll poll = GetPoll(vote.PollId);
			if (poll != null)
				ExpirePolls(poll.GroupId);

			PublicApi.PollVotes.Events.OnAfterDelete(vote);
		}

		internal static PollVote GetPollVote(Guid pollId, int userId)
		{
			var pollVote = PollingDataService.GetPollVote(pollId, userId);
			if (pollVote != null && GetPoll(pollVote.PollId) != null)
				return pollVote;

			return null;
		}

		internal static bool CanVote(Guid pollId, int userId)
		{
			var poll = GetPoll(pollId);
			if (poll == null)
				return false;

			return (!poll.VotingEndDateUtc.HasValue || poll.VotingEndDateUtc.Value >= DateTime.UtcNow) && CanVote(poll.GroupId);
		}

		internal static bool CanRead(Guid pollId, int userId)
		{
			var poll = GetPoll(pollId);
			if (poll == null)
				return false;

			return CanRead(poll.GroupId);
		}

		internal static bool CanModerate(Guid pollId, int userId)
		{
			var poll = GetPoll(pollId);
			if (poll == null)
				return false;

			return TEApi.NodePermissions.Get("groups", poll.GroupId, "Group_ModifyGroup").IsAllowed;
		}

		public static bool CanRead(int groupId)
		{
			return TEApi.NodePermissions.Get("groups", groupId, "Group_ReadGroup").IsAllowed;
		}

		public static bool CanVote(int groupId)
		{
			return !TEApi.Users.AccessingUser.IsSystemAccount.Value && CanRead(groupId);
		}

		internal static string RenderPollDescription(Poll poll, string target)
		{
			if (string.IsNullOrEmpty(target))
				target = "web";
			else
				target = target.ToLowerInvariant();
			
			if (target == "raw")
				return poll.Description ?? string.Empty;
			else
				return PublicApi.Polls.Events.OnRender(poll, "Description", poll.Description ?? string.Empty, target);
		}

		internal static string PollUrl(Guid pollId)
		{
			var poll = GetPoll(pollId);
			if (poll == null)
				return null;

			var group = TEApi.Groups.Get(new GroupsGetOptions { Id = poll.GroupId });
			if (group == null)
				return null;

			return TEApi.Url.Adjust(TEApi.GroupUrls.Custom(group.Id.Value, "poll"), string.Concat("PollId=", poll.Id.ToString()));
		}

		internal static string CreatePollUrl(int groupId, bool checkPermissions)
		{
			var group = TEApi.Groups.Get(new GroupsGetOptions { Id = groupId });
			if (group == null)
				return null;

			if (checkPermissions && !CanRead(groupId))
				return null;

			return TEApi.GroupUrls.Custom(group.Id.Value, "createeditpoll");
		}

		internal static string EditPollUrl(Guid pollId, bool checkPermissions)
		{
			var poll = GetPoll(pollId);
			if (poll == null)
				return null;

			var group = TEApi.Groups.Get(new GroupsGetOptions { Id = poll.GroupId });
			if (group == null)
				return null;

			if (checkPermissions && !CanModerate(pollId, TEApi.Users.AccessingUser.Id.Value))
				return null;

			return TEApi.Url.Adjust(TEApi.GroupUrls.Custom(group.Id.Value, "createeditpoll"), string.Concat("PollId=", poll.Id.ToString()));
		}

		internal static string PollListUrl(int groupId)
		{
			var group = TEApi.Groups.Get(new GroupsGetOptions { Id = groupId });
			if (group == null)
				return null;

			return TEApi.GroupUrls.Custom(group.Id.Value, "polls");
		}

		#region Validation

		private static void ValidatePoll(Poll poll)
		{
			if (poll.Id == Guid.Empty)
			{
				poll.CreatedDateUtc = DateTime.UtcNow;
				poll.Id = Guid.NewGuid();
			}

			poll.LastUpdatedDateUtc = DateTime.UtcNow;
			poll.Name = TEApi.Html.Sanitize(TEApi.Html.EnsureEncoded(poll.Name));
			poll.Description = TEApi.Html.Sanitize(poll.Description ?? string.Empty);

			if (poll.HideResultsUntilVotingComplete && !poll.VotingEndDateUtc.HasValue)
				poll.HideResultsUntilVotingComplete = false;

			if (string.IsNullOrEmpty(poll.Name))
				throw new InvalidOperationException("The name of the poll must be defined.");

			var group = TEApi.Groups.Get(new GroupsGetOptions { Id = poll.GroupId });
			if (group == null || group.HasErrors())
				throw new InvalidOperationException("The group identified on the poll is invalid.");

			if (!CanVote(group.Id.Value))
				throw new InvalidOperationException("The user does not have permission to create polls in this group.");

			if (poll.AuthorUserId <= 0)
				poll.AuthorUserId = TEApi.Users.AccessingUser.Id.Value;
			else if (poll.AuthorUserId != TEApi.Users.AccessingUser.Id.Value && !TEApi.NodePermissions.Get("groups", group.Id.Value, "Group_ModifyGroup").IsAllowed)
				throw new InvalidOperationException("The user does not have permission to create/edit this poll. The user must be the original creator or an admin in the group.");
		}

		private static void ValidatePollAnswer(PollAnswer answer)
		{
			if (answer.Id == Guid.Empty)
				answer.Id = Guid.NewGuid();

			answer.Name = TEApi.Html.Sanitize(TEApi.Html.EnsureEncoded(answer.Name));
			if (string.IsNullOrEmpty(answer.Name))
				throw new InvalidOperationException("The name of the poll answer must be defined.");

			Poll poll = GetPoll(answer.PollId);
			if (poll == null)
				throw new InvalidOperationException("The poll associated to the answer does not exist.");

			var group = TEApi.Groups.Get(new GroupsGetOptions { Id = poll.GroupId });
			if (group == null || group.HasErrors())
				throw new InvalidOperationException("The group identified on the poll is invalid.");

			if (poll.AuthorUserId != TEApi.Users.AccessingUser.Id.Value && !TEApi.NodePermissions.Get("groups", group.Id.Value, "Group_ModifyGroup").IsAllowed)
				throw new InvalidOperationException("The user does not have permission to create/edit this poll. The user must be the original creator or an admin in the group.");
		}

		private static void ValidatePollVote(PollVote vote)
		{
			if (vote.CreatedDateUtc == DateTime.MinValue)
				vote.CreatedDateUtc = DateTime.UtcNow;

			vote.LastUpdatedDateUtc = DateTime.UtcNow;

			Poll poll = GetPoll(vote.PollId);
			if (poll == null)
				throw new InvalidOperationException("The poll associated to the vote does not exist.");

			if (poll.VotingEndDateUtc.HasValue && poll.VotingEndDateUtc.Value < DateTime.UtcNow)
				throw new InvalidOperationException("Voting has ended. Votes cannot be added or changed.");

			if (!poll.Answers.Any(x => x.Id == vote.PollAnswerId))
				throw new InvalidOperationException("The poll answer doesn't exist on this poll.");

			var group = TEApi.Groups.Get(new GroupsGetOptions { Id = poll.GroupId });
			if (group == null || group.HasErrors())
				throw new InvalidOperationException("The group identified on the poll is invalid.");

			if (!CanVote(group.Id.Value))
				throw new InvalidOperationException("The user does not have permission to vote on polls in this group.");
		}

		#endregion

		#region Cache-related Methods

		private static void ExpirePoll(Guid pollId)
		{
			CacheService.Remove(PollCacheKey(pollId), CacheScope.All);
		}

		private static void ExpirePolls(int groupId)
		{
			CacheService.RemoveByTags(new string[] { PollTag(groupId) } , CacheScope.All);
		}

		private static string PollCacheKey(Guid pollId)
		{
			return string.Concat("Polling_PK_Poll:", pollId.ToString("N"));
		}

		private static string PollsCacheKey(int groupId, int pageSize, int pageIndex)
		{
			return string.Concat("Polling_PK_Polls:", groupId, ":", pageSize, ":", pageIndex);
		}

		private static string PollTag(int groupId)
		{
			return string.Concat("Polling_TAG_Group:", groupId);
		}

		#endregion
	}
}
