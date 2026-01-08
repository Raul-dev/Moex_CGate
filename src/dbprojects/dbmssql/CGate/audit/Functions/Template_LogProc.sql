CREATE   FUNCTION [audit].[Template_LogProc](
)RETURNS @LogProc TABLE (
    [ID] [bigint] IDENTITY(1,1) NOT NULL ,
    [LogID] [bigint] NOT NULL Primary Key,
	[Msg] [varchar](max) COLLATE Cyrillic_General_CI_AS NULL
)
AS
BEGIN
  RETURN
END