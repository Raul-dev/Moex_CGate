/*
Pre-deploy cleanup: drop legacy audit buffer tables replaced by PascalCase names.
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'[audit].[LogErrorBuffer]', N'U') IS NULL
   AND OBJECT_ID(N'[audit].[LogError_buffer]', N'U') IS NOT NULL
    DROP TABLE [audit].[LogError_buffer];

IF OBJECT_ID(N'[audit].[LogTextBuffer]', N'U') IS NULL
   AND OBJECT_ID(N'[audit].[LogText_buffer]', N'U') IS NOT NULL
    DROP TABLE [audit].[LogText_buffer];

GO
