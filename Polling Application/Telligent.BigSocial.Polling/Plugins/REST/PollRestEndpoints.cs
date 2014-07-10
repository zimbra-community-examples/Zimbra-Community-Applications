using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telligent.Evolution.Extensibility.Version1;
using Telligent.Evolution.Extensibility.Rest.Version2;
using Telligent.Evolution.Extensibility.Rest.Entities.Version2;
using Telligent.Evolution.Extensibility.Api.Version1;
using TEApi = Telligent.Evolution.Extensibility.Api.Version1.PublicApi;

namespace Telligent.BigSocial.Polling.Plugins
{
	public class PollRestEndpoints: IPlugin, IRestEndpoints
	{
		#region IPlugin Members

		public string Name
		{
			get { return "Poll REST API Endpoints";  }
		}

		public string Description
		{
			get { return "Adds support for Poll REST endpoints."; }
		}

		public void Initialize()
		{
		}

		#endregion

		#region IRestEndpoints Members

		public void Register(IRestEndpointController controller)
		{
			#region Poll Endpoints

			controller.Add(2, "groups/{groupid}/polls", new { }, new { groupid = @"\d+" }, HttpMethod.Get, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();
				response.Name = "Polls";

				try
				{
					int pageSize;
					int pageIndex;
					string sortBy = "Date";

					if (!int.TryParse(request.Request.QueryString["PageSize"], out pageSize))
						pageSize = 20;

					if (!int.TryParse(request.Request.QueryString["PageIndex"], out pageIndex))
						pageIndex = 0;

					if (request.Request.QueryString["SortBy"] != null)
						sortBy = request.Request.QueryString["SortBy"];

					if (sortBy == "TopPollsScore")
					{
						var group = TEApi.Groups.Get(new GroupsGetOptions { Id = Convert.ToInt32(request.PathParameters["groupid"]) });
						if (group == null || group.HasErrors())
							response.Data = new Telligent.Evolution.Extensibility.Rest.Entities.Version1.PagedList<RestApi.Poll>();
						else
						{
							var scores = TEApi.CalculatedScores.List(Plugins.TopPollsScore.ScoreId, new CalculatedScoreListOptions { ApplicationId = group.ApplicationId, ContentTypeId = PublicApi.Polls.ContentTypeId, PageIndex = pageIndex, PageSize = pageSize, SortOrder = "Descending" });

							var polls = new List<RestApi.Poll>();
							foreach (var score in scores)
							{
								if (score.Content != null)
								{
									var poll = InternalApi.PollingService.GetPoll(score.Content.ContentId);
									if (poll != null)
										polls.Add(new RestApi.Poll(poll));
								}
							}

							response.Data = new Telligent.Evolution.Extensibility.Rest.Entities.Version1.PagedList<RestApi.Poll>(polls, scores.PageSize, scores.PageIndex, scores.TotalCount);
						}
					}
					else
					{
						var polls = InternalApi.PollingService.ListPolls(Convert.ToInt32(request.PathParameters["groupid"]), pageSize, pageIndex);
						response.Data = new Telligent.Evolution.Extensibility.Rest.Entities.Version1.PagedList<RestApi.Poll>(polls.Select(x => new RestApi.Poll(x)), polls.PageSize, polls.PageIndex, polls.TotalCount);
					}
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation { 
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Lists Polls.", Resource = "Poll", Action = "List" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "groupid", Description = "The identifier of the group from which to retrieve polls", Type = typeof(int), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.Path },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "PageSize", Description = "The size of the page of results", Type = typeof(int), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Optional, Default = 20, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.QueryString },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "PageIndex", Description = "The zero-based index of the page to display", Type = typeof(int), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Optional, Default = 0, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.QueryString },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "SortBy", Description = "The field to sort by", Type = typeof(string), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Optional, Default = "Date", Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.QueryString, Options = new string[] { "Date", "TopPollsScore" } }
				},
				ResponseDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestResponseDocumentationAttribute { Name = "Polls", Description = "The polls matching the listing criteria", Type = typeof(Telligent.Evolution.Extensibility.Rest.Entities.Version1.PagedList<RestApi.Poll>) }
			});

			controller.Add(2, "polls/poll", HttpMethod.Get, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();
				response.Name = "Poll";

				try
				{
					Guid pollId;
					if (!Guid.TryParse(request.Request.QueryString["Id"], out pollId))
						throw new ArgumentException("Id is required.");

					var poll = InternalApi.PollingService.GetPoll(pollId);
					if (poll == null)
						throw new Exception("The poll does not exist.");
					
					response.Data = new RestApi.Poll(poll);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Get a Poll.", Resource = "Poll", Action = "Show" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Id", Description = "The identifier of the poll to retrieve", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.QueryString }
				},
				ResponseDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestResponseDocumentationAttribute { Name = "Poll", Description = "The poll.", Type = typeof(RestApi.Poll) }
			});

			controller.Add(2, "polls/poll", HttpMethod.Delete, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();

				try
				{
					Guid pollId;
					if (!Guid.TryParse(request.Request.QueryString["Id"], out pollId))
						throw new ArgumentException("Id is required.");

					var poll = InternalApi.PollingService.GetPoll(pollId);
					if (poll != null)
						InternalApi.PollingService.DeletePoll(poll);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Deletes a Poll.", Resource = "Poll", Action = "Delete" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Id", Description = "The identifier of the poll to delete.", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.QueryString }
				}
			});

			controller.Add(2, "polls/poll", HttpMethod.Post, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();
				response.Name = "Poll";

				try
				{
					// Create
					int groupId;
					if (!int.TryParse(request.Form["GroupId"], out groupId))
						throw new ArgumentException("GroupId is required.");

					string name = request.Form["Name"] ?? string.Empty;
					string description = request.Form["Description"];
					bool hideResultsUntilVotingComplete = request.Form["HideResultsUntilVotingComplete"] == null ? false : Convert.ToBoolean(request.Form["HideResultsUntilVotingComplete"]);
					DateTime? votingEndDate = request.Form["VotingEndDate"] == null ? null : (DateTime?)InternalApi.Formatting.FromUserTimeToUtc(DateTime.Parse(request.Form["VotingEndDate"]));

					var poll = new InternalApi.Poll();
					poll.GroupId = groupId;
					poll.Name = name;
					poll.Description = description;
					poll.IsEnabled = true;
					poll.AuthorUserId = request.UserId;
					poll.HideResultsUntilVotingComplete = hideResultsUntilVotingComplete;
					poll.VotingEndDateUtc = votingEndDate;

					InternalApi.PollingService.AddUpdatePoll(poll);
					poll = InternalApi.PollingService.GetPoll(poll.Id);
					
					response.Data = new RestApi.Poll(poll);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Create a Poll.", Resource = "Poll", Action = "Create" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "GroupId", Description = "The identifier of the group to save this poll within", Type = typeof(int), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Name", Description = "The name of the poll", Type = typeof(string), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Description", Description = "The description of the poll", Type = typeof(string), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Optional, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "HideResultsUntilVotingComplete", Description = "If true, results will not be shown until voting is complete", Type = typeof(bool), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Optional, Default = false, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "VotingEndDate", Description = "The date to stop allowing voting", Type = typeof(DateTime), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Optional, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody }
				},
				ResponseDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestResponseDocumentationAttribute { Name = "Poll", Description = "The newly created poll", Type = typeof(RestApi.Poll) }
			});

			controller.Add(2, "polls/poll", HttpMethod.Put, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();
				response.Name = "Poll";

				try
				{
					// Update
					Guid pollId;
					if (!Guid.TryParse(request.Request.QueryString["Id"], out pollId))
						throw new ArgumentException("Id is required.");

					string name = request.Form["Name"];
					string description = request.Form["Description"];

					var poll = InternalApi.PollingService.GetPoll(pollId);
					if (poll == null)
						throw new Exception("The poll does not exist.");

					if (request.Form["HideResultsUntilVotingComplete"] != null)
						poll.HideResultsUntilVotingComplete = Convert.ToBoolean(request.Form["HideResultsUntilVotingComplete"]);

					if (request.Form["VotingEndDate"] != null)
						poll.VotingEndDateUtc = (DateTime?) InternalApi.Formatting.FromUserTimeToUtc(Convert.ToDateTime(request.Form["VotingEndDate"]));
					else if (request.Form["ClearVotingEndDate"] != null && Convert.ToBoolean(request.Form["ClearVotingEndDate"]))
						poll.VotingEndDateUtc = null;
					
					if (name != null)
						poll.Name = name;

					if (description != null)
						poll.Description = description;

					InternalApi.PollingService.AddUpdatePoll(poll);
					poll = InternalApi.PollingService.GetPoll(poll.Id);

					response.Data = new RestApi.Poll(poll);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Update a Poll.", Resource = "Poll", Action = "Update" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Id", Description = "The identifier of the poll to update", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.QueryString },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Name", Description = "The name of the poll", Type = typeof(string), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Optional, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Description", Description = "The description of the poll", Type = typeof(string), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Optional, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "HideResultsUntilVotingComplete", Description = "If true, results will not be shown until voting is complete", Type = typeof(bool), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Optional, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "VotingEndDate", Description = "The date to stop allowing voting", Type = typeof(DateTime), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Optional, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody }
				},
				ResponseDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestResponseDocumentationAttribute { Name = "Poll", Description = "The updated poll", Type = typeof(RestApi.Poll) }
			});

			#endregion

			#region Poll Answer Endpoints

			controller.Add(2, "polls/answer", HttpMethod.Get, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();
				response.Name = "PollAnswer";

				try
				{
					Guid pollAnswerId;
					if (!Guid.TryParse(request.Request.QueryString["Id"], out pollAnswerId))
						throw new ArgumentException("Id is required.");

					var pollAnswer = InternalApi.PollingService.GetPollAnswer(pollAnswerId);
					if (pollAnswer == null)
						throw new Exception("The poll answer does not exist.");

					var poll = InternalApi.PollingService.GetPoll(pollAnswer.PollId);
					if (poll == null)
						throw new Exception("The poll does not exist.");

					response.Data = new RestApi.PollAnswer(pollAnswer, poll);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Gets a Poll Answer", Resource = "PollAnswer", Action = "Show" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Id", Description = "The identifier of the poll answer to retrieve", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.QueryString }
				},
				ResponseDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestResponseDocumentationAttribute { Name = "PollAnswer", Description = "The poll answer", Type = typeof(RestApi.PollAnswer) }
			});

			controller.Add(2, "polls/answer", HttpMethod.Delete, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();

				try
				{
					Guid pollAnswerId;
					if (!Guid.TryParse(request.Request.QueryString["Id"], out pollAnswerId))
						throw new ArgumentException("Id is required.");

					var pollAnswer = InternalApi.PollingService.GetPollAnswer(pollAnswerId);
					if (pollAnswer != null)
						InternalApi.PollingService.DeletePollAnswer(pollAnswer);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Delete a Poll Answer.", Resource = "PollAnswer", Action = "Delete" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Id", Description = "The identifier of the poll answer to delete", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.QueryString }
				}
			});

			controller.Add(2, "polls/answer", HttpMethod.Post, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();
				response.Name = "PollAnswer";

				try
				{
					// Create
					Guid pollId;
					if (!Guid.TryParse(request.Form["PollId"], out pollId))
						throw new ArgumentException("PollId is required.");

					string name = request.Form["Name"] ?? string.Empty;

					var pollAnswer = new InternalApi.PollAnswer();
					pollAnswer.PollId = pollId;
					pollAnswer.Name = name;

					InternalApi.PollingService.AddUpdatePollAnswer(pollAnswer);
					pollAnswer = InternalApi.PollingService.GetPollAnswer(pollAnswer.Id);

					var poll = InternalApi.PollingService.GetPoll(pollAnswer.PollId);

					response.Data = new RestApi.PollAnswer(pollAnswer, poll);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Create a Poll Answer.", Resource = "PollAnswer", Action = "Create" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "PollId", Description = "The identifier of the poll to save this answer within", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Name", Description = "The name of the poll answer", Type = typeof(string), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody }
				},
				ResponseDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestResponseDocumentationAttribute { Name = "PollAnswer", Description = "The newly created poll answer", Type = typeof(RestApi.PollAnswer) }
			});

			controller.Add(2, "polls/answer", HttpMethod.Put, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();
				response.Name = "PollAnswer";

				try
				{
					// Update
					Guid pollAnswerId;
					if (!Guid.TryParse(request.Request.QueryString["Id"], out pollAnswerId))
						throw new ArgumentException("Id is required.");

					string name = request.Form["Name"];

					var pollAnswer = InternalApi.PollingService.GetPollAnswer(pollAnswerId);
					if (pollAnswer == null)
						throw new Exception("The poll answer does not exist.");

					if (name != null)
						pollAnswer.Name = name;

					InternalApi.PollingService.AddUpdatePollAnswer(pollAnswer);
					pollAnswer = InternalApi.PollingService.GetPollAnswer(pollAnswer.Id);

					var poll = InternalApi.PollingService.GetPoll(pollAnswer.PollId);

					response.Data = new RestApi.PollAnswer(pollAnswer, poll);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Update a Poll Answer.", Resource = "PollAnswer", Action = "Update" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Id", Description = "The identifier of the poll answer to update", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.QueryString },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "Name", Description = "The name of the poll", Type = typeof(string), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody }
				},
				ResponseDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestResponseDocumentationAttribute { Name = "PollAnswer", Description = "The updated poll answer", Type = typeof(RestApi.PollAnswer) }
			});

			#endregion

			#region Poll Voting Endpoints

			controller.Add(2, "polls/vote", HttpMethod.Get, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();
				response.Name = "PollVote";

				try
				{
					Guid pollId;
					if (!Guid.TryParse(request.Request.QueryString["PollId"], out pollId))
						throw new ArgumentException("PollId is required.");

					var poll = InternalApi.PollingService.GetPoll(pollId);
					if (poll == null)
						throw new Exception("The poll does not exist.");

					var vote = InternalApi.PollingService.GetPollVote(pollId, request.UserId);
					if (vote != null)
						response.Data = new RestApi.PollVote(vote);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Gets a Poll Vote.", Resource = "PollVote", Action = "Show" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "PollId", Description = "The identifier of the poll for which to retrieve the accessing user's vote", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.QueryString }
				},
				ResponseDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestResponseDocumentationAttribute { Name = "PollVote", Description = "The accessing user's vote", Type = typeof(RestApi.PollVote) }
			});

			controller.Add(2, "polls/vote", HttpMethod.Delete, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();

				try
				{
					Guid pollId;
					if (!Guid.TryParse(request.Form["PollId"], out pollId))
						throw new ArgumentException("PollId is required.");

					var vote = InternalApi.PollingService.GetPollVote(pollId, request.UserId);
					if (vote != null)
						InternalApi.PollingService.DeletePollVote(vote);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Delete a Poll Vote.", Resource = "PollVote", Action = "Delete" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "PollId", Description = "The identifier of the poll for which to delete the accessing user's vote", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody }
				}
			});

			controller.Add(2, "polls/vote", HttpMethod.Post, (IRestRequest request) =>
			{
				var response = new RestApi.RestResponse();
				response.Name = "PollVote";

				try
				{
					// Create
					Guid pollId;
					if (!Guid.TryParse(request.Form["PollId"], out pollId))
						throw new ArgumentException("PollId is required.");

					Guid pollAnswerId;
					if (!Guid.TryParse(request.Form["PollAnswerId"], out pollAnswerId))
						throw new ArgumentException("PollAnswerId is required.");
					
					var pollVote = new InternalApi.PollVote();
					pollVote.PollId = pollId;
					pollVote.PollAnswerId = pollAnswerId;
					pollVote.UserId = request.UserId;

					InternalApi.PollingService.AddUpdatePollVote(pollVote);
					pollVote = InternalApi.PollingService.GetPollVote(pollId, request.UserId);

					response.Data = new RestApi.PollVote(pollVote);
				}
				catch (Exception ex)
				{
					response.Errors = new string[] { ex.Message };
				}

				return response;
			}, new RestEndpointDocumentation
			{
				EndpointDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestEndpointDocumentationAttribute { Description = "Create or update a Poll Vote.", Resource = "PollVote", Action = "Vote" },
				RequestDocumentation = new List<Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute> { 
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "PollId", Description = "The identifier of the poll being voted on by the accessing user", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody },
					new Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequestDocumentationAttribute { Name = "PollAnswerId", Description = "The identifier of the selected poll answer", Type = typeof(Guid), Required = Evolution.Extensibility.Rest.Infrastructure.Version1.RestRequired.Required, Location = Evolution.Extensibility.Rest.Infrastructure.Version1.RestParameterLocation.RequestBody }
				},
				ResponseDocumentation = new Evolution.Extensibility.Rest.Infrastructure.Version1.RestResponseDocumentationAttribute { Name = "PollVote", Description = "The accessing user's vote", Type = typeof(RestApi.PollVote) }
			});

			#endregion
		}

		#endregion
	}
}
