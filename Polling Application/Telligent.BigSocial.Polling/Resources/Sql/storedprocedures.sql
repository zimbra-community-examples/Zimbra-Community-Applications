﻿/****** Object:  StoredProcedure [dbo].[polling_PollAnswer_UpdateVoteCounts]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_PollAnswer_UpdateVoteCounts]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_PollAnswer_UpdateVoteCounts]
GO
CREATE PROCEDURE [dbo].[polling_PollAnswer_UpdateVoteCounts]
	@PollId uniqueidentifier = null
AS
BEGIN
	SET NOCOUNT ON;

    UPDATE pa
    SET VoteCount = (
		SELECT COUNT(*)
		FROM polling_PollVotes pv
		WHERE pv.PollId = pa.PollId 
			AND pv.PollAnswerId = pa.Id
    )
    FROM polling_PollAnswers pa
    WHERE pa.PollId = @PollId OR @PollId is null

	UPDATE p
	SET LastUpdatedDateUtc = GETUTCDATE()
	FROM polling_Polls p
	WHERE p.Id = @PollId
    
END
GO
/****** Object:  StoredProcedure [dbo].[polling_PollAnswer_Get]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_PollAnswer_Get]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_PollAnswer_Get]
GO
CREATE PROCEDURE [dbo].[polling_PollAnswer_Get]
	@Id uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;

    SELECT Id, PollId, Name, VoteCount
    FROM polling_PollAnswers
    WHERE Id = @Id
END
GO
/****** Object:  StoredProcedure [dbo].[polling_PollAnswer_Delete]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_PollAnswer_Delete]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_PollAnswer_Delete]
GO
CREATE PROCEDURE [dbo].[polling_PollAnswer_Delete]
	@Id uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @Err int
	DECLARE @RowCount int
	DECLARE @PollId uniqueidentifier
	
	BEGIN TRAN

		SELECT @PollId = PollId
		FROM polling_PollAnswers
		WHERE Id = @Id

		IF @@ROWCOUNT = 1 BEGIN
	
			DELETE FROM polling_PollAnswers
			WHERE Id = @Id
		
			DELETE FROM polling_PollVotes
			WHERE PollAnswerId = @Id

			EXEC polling_PollAnswer_UpdateVoteCounts @PollId = @PollId

		END
		
	COMMIT TRAN

END
GO
/****** Object:  StoredProcedure [dbo].[polling_PollAnswer_AddUpdate]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_PollAnswer_AddUpdate]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_PollAnswer_AddUpdate]
GO
CREATE PROCEDURE [dbo].[polling_PollAnswer_AddUpdate]
	@Id uniqueidentifier,
	@PollId uniqueidentifier,
	@Name nvarchar(255)
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @Err int
	DECLARE @RowCount int
	
	BEGIN TRAN
	
		UPDATE pa
		SET Name = @Name
		FROM polling_PollAnswers pa
		WHERE pa.Id = @Id
		
		SELECT @Err = @@ERROR, @RowCount = @@ROWCOUNT
		
		IF @Err <> 0 BEGIN
			ROLLBACK TRAN
			RETURN
		END
		
		IF @RowCount = 0 BEGIN
		
			INSERT INTO polling_PollAnswers (Id, PollId, Name, VoteCount)
			VALUES (@Id, @PollId, @Name, 0)
			
			SET @Err = @@ERROR
			IF @Err <> 0 BEGIN
				ROLLBACK TRAN
				RETURN
			END
		
		END

		EXEC polling_PollAnswer_UpdateVoteCounts @PollId = @PollId
		
	COMMIT TRAN

END
GO
/****** Object:  StoredProcedure [dbo].[polling_Poll_SetIndexed]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_Poll_SetIndexed]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_Poll_SetIndexed]
GO
CREATE PROCEDURE [dbo].[polling_Poll_SetIndexed]
	@PollIds XML
AS
BEGIN

	SET NOCOUNT ON

	UPDATE p
	SET IsIndexed = 1
	FROM polling_Polls p
	WHERE Id IN (
		SELECT Tbl.Col.value('@Id','UNIQUEIDENTIFIER')
		FROM @PollIds.nodes('//Poll') Tbl(Col)
	)
	
END
GO
/****** Object:  StoredProcedure [dbo].[polling_Poll_GetToReindex]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_Poll_GetToReindex]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_Poll_GetToReindex]
GO
CREATE PROCEDURE [dbo].[polling_Poll_GetToReindex]
	@PagingBegin int,
	@PagingEnd int,
	@TotalRecords int = null output
AS
BEGIN
	SET NOCOUNT ON;

    SET @TotalRecords = 0
	
	DECLARE @tblPageIndex TABLE
	(
		IndexId int not null PRIMARY KEY CLUSTERED,
		PollId uniqueidentifier NOT NULL,
		TotalRecords int NOT NULL
	)

	INSERT INTO @tblPageIndex (IndexId, PollId, TotalRecords)
	SELECT i.RowId, i.PollId, i.TotalRecords
	FROM (
		SELECT ROW_NUMBER() OVER (ORDER BY p.LastUpdatedDateUtc ASC) AS RowId, p.Id as PollId, COUNT(*) OVER () AS TotalRecords
		FROM polling_Polls p
		WHERE p.IsIndexed = 0
		) i
	WHERE i.RowId > @PagingBegin
		AND i.RowId <= @PagingEnd
	ORDER BY i.RowId ASC
	
	SELECT @TotalRecords = COALESCE(NULLIF((SELECT TOP 1 TotalRecords FROM @tblPageIndex), 0), 0)
	
	SELECT p.Id, p.Name, p.Description, p.AuthorUserId, p.GroupId, p.IsEnabled, p.IsIndexed, p.CreatedDateUtc, p.LastUpdatedDateUtc, p.HideResultsUntilVotingComplete, p.VotingEndDateUtc
	FROM polling_Polls p
	INNER JOIN @tblPageIndex i ON i.PollId = p.Id
	ORDER BY i.IndexId
	
	SELECT pa.Id, pa.PollId, pa.Name, pa.VoteCount
	FROM polling_PollAnswers pa
	INNER JOIN @tblPageIndex i ON i.PollId = pa.PollId
	ORDER BY i.IndexId
END
GO
/****** Object:  StoredProcedure [dbo].[polling_Poll_GetSet]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_Poll_GetSet]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_Poll_GetSet]
GO
CREATE PROCEDURE [dbo].[polling_Poll_GetSet]
	@GroupId int,
	@PagingBegin int,
	@PagingEnd int,
	@TotalRecords int = null output
AS
BEGIN
	SET NOCOUNT ON;

	SET @TotalRecords = 0
	
	DECLARE @tblPageIndex TABLE
	(
		IndexId int not null PRIMARY KEY CLUSTERED,
		PollId uniqueidentifier NOT NULL,
		TotalRecords int NOT NULL
	)

	INSERT INTO @tblPageIndex (IndexId, PollId, TotalRecords)
	SELECT i.RowId, i.PollId, i.TotalRecords
	FROM (
		SELECT ROW_NUMBER() OVER (ORDER BY p.CreatedDateUtc DESC) AS RowId, p.Id as PollId, COUNT(*) OVER () AS TotalRecords
		FROM polling_Polls p
		WHERE p.GroupId = @GroupId
			AND IsEnabled = 1
		) i
	WHERE i.RowId > @PagingBegin
		AND i.RowId <= @PagingEnd
	ORDER BY i.RowId ASC
	
	SELECT @TotalRecords = COALESCE(NULLIF((SELECT TOP 1 TotalRecords FROM @tblPageIndex), 0), 0)
	
	SELECT p.Id, p.Name, p.Description, p.AuthorUserId, p.GroupId, p.IsEnabled, p.IsIndexed, p.CreatedDateUtc, p.LastUpdatedDateUtc, p.HideResultsUntilVotingComplete, p.VotingEndDateUtc
	FROM polling_Polls p
	INNER JOIN @tblPageIndex i ON i.PollId = p.Id
	ORDER BY i.IndexId
	
	SELECT pa.Id, pa.PollId, pa.Name, pa.VoteCount
	FROM polling_PollAnswers pa
	INNER JOIN @tblPageIndex i ON i.PollId = pa.PollId
	ORDER BY i.IndexId
END
GO
/****** Object:  StoredProcedure [dbo].[polling_Poll_Get]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_Poll_Get]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_Poll_Get]
GO
CREATE PROCEDURE [dbo].[polling_Poll_Get]
	@Id uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON

	SELECT Id, Name, Description, AuthorUserId, GroupId, IsEnabled, IsIndexed, CreatedDateUtc, LastUpdatedDateUtc, HideResultsUntilVotingComplete, VotingEndDateUtc
	FROM polling_Polls
	WHERE Id = @Id
	
	SELECT Id, PollId, Name, VoteCount
	FROM polling_PollAnswers
	WHERE PollId = @Id

END
GO
/****** Object:  StoredProcedure [dbo].[polling_Poll_Delete]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_Poll_Delete]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_Poll_Delete]
GO
CREATE PROCEDURE [dbo].[polling_Poll_Delete]
	@Id uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @Err int
	DECLARE @RowCount int
	
	BEGIN TRAN
	
		DELETE FROM polling_Polls
		WHERE Id = @Id
	
		DELETE FROM polling_PollAnswers
		WHERE PollId = @Id
		
		DELETE FROM polling_PollVotes
		WHERE PollId = @Id
		
	COMMIT TRAN

END
GO
/****** Object:  StoredProcedure [dbo].[polling_Poll_AddUpdate]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_Poll_AddUpdate]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_Poll_AddUpdate]
GO
CREATE PROCEDURE [dbo].[polling_Poll_AddUpdate]
	@Id uniqueidentifier,
	@Name nvarchar(255),
	@Description nvarchar(max) = null,
	@AuthorUserId int,
	@GroupId int,
	@IsEnabled bit = 1,
	@DateUtc datetime,
	@HideResultsUntilVotingComplete bit = 0,
	@VotingEndDateUtc datetime = null
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @Err int
	DECLARE @RowCount int
	
	BEGIN TRAN
	
		UPDATE p
		SET Name = @Name,
			Description = @Description,
			AuthorUserId = @AuthorUserId,
			GroupId = @GroupId,
			IsEnabled = @IsEnabled,
			LastUpdatedDateUtc = @DateUtc,
			HideResultsUntilVotingComplete = @HideResultsUntilVotingComplete,
			VotingEndDateUtc = @VotingEndDateUtc
		FROM polling_Polls p
		WHERE p.Id = @Id
		
		SELECT @Err = @@ERROR, @RowCount = @@ROWCOUNT
		
		IF @Err <> 0 BEGIN
			ROLLBACK TRAN
			RETURN
		END
		
		IF @RowCount = 0 BEGIN
		
			INSERT INTO polling_Polls (Id, Name, Description, AuthorUserId, GroupId, IsEnabled, CreatedDateUtc, LastUpdatedDateUtc, HideResultsUntilVotingComplete, VotingEndDateUtc)
			VALUES (@Id, @Name, @Description, @AuthorUserId, @GroupId, @IsEnabled, @DateUtc, @DateUtc, @HideResultsUntilVotingComplete, @VotingEndDateUtc)
			
			SET @Err = @@ERROR
			IF @Err <> 0 BEGIN
				ROLLBACK TRAN
				RETURN
			END
		
		END
		
	COMMIT TRAN

END
GO
/****** Object:  StoredProcedure [dbo].[polling_PollVote_Get]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_PollVote_Get]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_PollVote_Get]
GO
CREATE PROCEDURE [dbo].[polling_PollVote_Get]
	@PollId uniqueidentifier,
	@UserId int
AS
BEGIN
	SET NOCOUNT ON

	SELECT PollId, UserId, PollAnswerId, CreatedDateUtc, LastUpdatedDateUtc
	FROM polling_PollVotes
	WHERE PollId = @PollId 
		and UserId = @UserId

END
GO
/****** Object:  StoredProcedure [dbo].[polling_PollVote_Delete]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_PollVote_Delete]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_PollVote_Delete]
GO
CREATE PROCEDURE [dbo].[polling_PollVote_Delete]
	@PollId uniqueidentifier,
	@UserId int
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @Err int
	DECLARE @RowCount int
	
	BEGIN TRAN
	
		DELETE FROM polling_PollVotes
		WHERE PollId = @PollId
			and UserId = @UserId
		
		EXEC polling_PollAnswer_UpdateVoteCounts @PollId = @PollId
		
	COMMIT TRAN

END
GO
/****** Object:  StoredProcedure [dbo].[polling_PollVote_AddUpdate]    Script Date: 08/27/2012 13:27:22 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_PollVote_AddUpdate]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].[polling_PollVote_AddUpdate]
GO
CREATE PROCEDURE [dbo].[polling_PollVote_AddUpdate]
	@PollId uniqueidentifier,
	@UserId int,
	@PollAnswerId uniqueidentifier,
	@DateUtc datetime
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @Err int
	DECLARE @RowCount int
	
	BEGIN TRAN
	
		UPDATE pv
		SET PollAnswerId = @PollAnswerId,
			LastUpdatedDateUtc = @DateUtc
		FROM polling_PollVotes pv
		WHERE pv.PollId = @PollId
			and pv.UserId = @UserId
		
		SELECT @Err = @@ERROR, @RowCount = @@ROWCOUNT
		
		IF @Err <> 0 BEGIN
			ROLLBACK TRAN
			RETURN
		END
		
		IF @RowCount = 0 BEGIN
		
			INSERT INTO polling_PollVotes (PollId, UserId, PollAnswerId, CreatedDateUtc, LastUpdatedDateUtc)
			VALUES (@PollId, @UserId, @PollAnswerId, @DateUtc, @DateUtc)
			
			SET @Err = @@ERROR
			IF @Err <> 0 BEGIN
				ROLLBACK TRAN
				RETURN
			END
		
		END
		
		EXEC polling_PollAnswer_UpdateVoteCounts @PollId = @PollId
		
	COMMIT TRAN

END
GO
/****** Object:  StoredProcedure [dbo].[polling_Poll_DeleteByGroup]     ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_Poll_DeleteByGroup]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].polling_Poll_DeleteByGroup
GO
CREATE PROCEDURE [dbo].polling_Poll_DeleteByGroup
	@Id int
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @Err int
	DECLARE @RowCount int
	
	BEGIN TRAN
	
		DELETE FROM polling_Polls
		WHERE [GroupId] = @Id
	
		DELETE FROM polling_PollAnswers
		WHERE PollId not in (SELECT Id from polling_Polls)
		
		DELETE FROM polling_PollVotes
		WHERE PollId not in (SELECT Id from polling_Polls)
		
	COMMIT TRAN

END
GO
SET ANSI_NULLS ON
/****** Object:  StoredProcedure [dbo].[polling_ReassignUser]     ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF EXISTS ( SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'dbo.[polling_ReassignUser]') AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )
	DROP PROCEDURE [dbo].polling_ReassignUser
GO
CREATE PROCEDURE [dbo].polling_ReassignUser
	@OldUserId int,
	@NewUserId int
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @Err int
	DECLARE @RowCount int
	
	BEGIN TRAN
	
		UPDATE polling_Polls
		SET AuthorUserId = @NewUserId,
			IsIndexed = 0
		WHERE AuthorUserId = @OldUserId

		DELETE FROM polling_PollVotes
		WHERE UserId = @OldUserId 
			AND PollId in (
				SELECT pv1.PollId
				FROM polling_PollVotes pv1
				INNER JOIN polling_PollVotes pv2 on pv1.PollId = pv2.PollId
				WHERE pv1.UserId = @OldUserId
				AND pv2.UserId = @NewUserId
			)

		UPDATE polling_PollVotes
		SET UserId = @NewUserId
		WHERE UserId = @OldUserId

		EXEC [polling_PollAnswer_UpdateVoteCounts] @PollId = null
		
	COMMIT TRAN

END
GO