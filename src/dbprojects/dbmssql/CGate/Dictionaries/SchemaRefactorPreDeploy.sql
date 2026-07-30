/*
Pre-deploy cleanup: drop legacy dbo/crs/audit objects replaced by mq + PascalCase names.
Runs before dacpac publish on existing databases.
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'[audit].[LogErrorBuffer]', N'U') IS NULL
   AND OBJECT_ID(N'[audit].[LogError_buffer]', N'U') IS NOT NULL
    DROP TABLE [audit].[LogError_buffer];

IF OBJECT_ID(N'[audit].[LogTextBuffer]', N'U') IS NULL
   AND OBJECT_ID(N'[audit].[LogText_buffer]', N'U') IS NOT NULL
    DROP TABLE [audit].[LogText_buffer];

IF OBJECT_ID(N'[crs].[OrdersLogBuffer]', N'U') IS NULL
   AND OBJECT_ID(N'[crs].[orders_log_buffer]', N'U') IS NOT NULL
    DROP TABLE [crs].[orders_log_buffer];

IF OBJECT_ID(N'[mq].[MessageQueue]', N'U') IS NULL
   AND OBJECT_ID(N'[dbo].[msgqueue]', N'U') IS NOT NULL
    DROP TABLE [dbo].[msgqueue];

IF OBJECT_ID(N'[mq].[MetaMap]', N'U') IS NULL
   AND OBJECT_ID(N'[dbo].[metamap]', N'U') IS NOT NULL
    DROP TABLE [dbo].[metamap];

IF OBJECT_ID(N'[mq].[MetaAdapter]', N'U') IS NULL
   AND OBJECT_ID(N'[dbo].[metaadapter]', N'U') IS NOT NULL
    DROP TABLE [dbo].[metaadapter];

IF OBJECT_ID(N'[mq].[MessageType]', N'U') IS NULL
   AND OBJECT_ID(N'[dbo].[msgtype]', N'U') IS NOT NULL
    DROP TABLE [dbo].[msgtype];

IF OBJECT_ID(N'[mq].[DataSource]', N'U') IS NULL
   AND OBJECT_ID(N'[dbo].[data_source]', N'U') IS NOT NULL
    DROP TABLE [dbo].[data_source];

IF OBJECT_ID(N'[mq].[SessionLog]', N'U') IS NULL
   AND OBJECT_ID(N'[dbo].[session_log]', N'U') IS NOT NULL
    DROP TABLE [dbo].[session_log];

IF OBJECT_ID(N'[mq].[Session]', N'U') IS NULL
   AND OBJECT_ID(N'[dbo].[session]', N'U') IS NOT NULL
    DROP TABLE [dbo].[session];

IF OBJECT_ID(N'[mq].[SessionState]', N'U') IS NULL
   AND OBJECT_ID(N'[dbo].[session_state]', N'U') IS NOT NULL
    DROP TABLE [dbo].[session_state];

IF OBJECT_ID(N'[mq].[sp_SaveSessionState]', N'P') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[rb_SaveSessionState]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[rb_SaveSessionState];

IF OBJECT_ID(N'[dbo].[sp_SaveSessionState]', N'P') IS NOT NULL
   AND OBJECT_ID(N'[mq].[sp_SaveSessionState]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_SaveSessionState];

GO
