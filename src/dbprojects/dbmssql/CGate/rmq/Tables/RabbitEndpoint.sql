CREATE TABLE [rmq].[RabbitEndpoint] (
    [EndpointID]         INT             IDENTITY (1, 1) NOT NULL,
    [AliasName]          NVARCHAR (128)  NOT NULL,
    [ServerName]         VARCHAR (512)   NOT NULL,
    [Port]               INT             CONSTRAINT [df_RabbitEndpoint_Port] DEFAULT ((5672)) NOT NULL,
    [VHost]              NVARCHAR (256)  CONSTRAINT [df_RabbitEndpoint_VHost] DEFAULT ('/') NOT NULL,
    [LoginName]          VARCHAR (256)   NOT NULL,
    [LoginPassword]      VARBINARY (128) NOT NULL,
    [Exchange]           VARCHAR (128)   NOT NULL,
    [RoutingKey]         VARCHAR (256)   NULL,
    [ConnectionChannels] INT             CONSTRAINT [df_RabbitEndpoint_ConnectionChannels] DEFAULT ((5)) NOT NULL,
    [IsEnabled]          BIT             CONSTRAINT [df_RemoteServer_IsEnabled] DEFAULT ((1)) NOT NULL,
    CONSTRAINT [pk_EndpointID] PRIMARY KEY CLUSTERED ([EndpointID] ASC)
);

