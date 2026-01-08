--EXEC audit.sp_Initialise @AuditType = 'Rabbit'
CREATE PROCEDURE audit.sp_Initialise (
  @AuditType varchar(256) = NULL,  -- R = 'Rabbit', L='LinkedServer'
  @IsEnable  bit = 1
)
AS
BEGIN
  SET NOCOUNT ON;
  IF @IsEnable = 0 BEGIN
    DELETE FROM [audit].[Setting] WHERE [ID] in( 1, 2, 3)
    RETURN
  END

  IF @AuditType  IN ('R', 'Rabbit') BEGIN
      BEGIN TRY
      EXEC [rmq].[sp_clr_InitialiseRabbitMq]

      DELETE FROM [audit].[Setting] WHERE [ID] in( 1, 2, 3)
      INSERT [audit].[Setting] ([ID], [IntValue], [Code], [StrValue])
      VALUES (1,3,'AuditSPTypeDefault', 'Rabbit'),
             (2,3,'AuditLTTypeDefault', 'Rabbit'),
             (3,3,'AuditErrTypeDefault', 'Rabbit')

        EXEC [audit].[sp_Print] 'Instaled Audit Type: Rabbit'
      END TRY
      BEGIN CATCH
        DECLARE @ErrorMessage nvarchar(max)
        SET @ErrorMessage = ERROR_MESSAGE()
        EXEC [audit].[sp_Print] @ErrorMessage
        DELETE FROM [audit].[Setting] WHERE [ID] in( 1, 2, 3)
      END CATCH
  END
  IF @AuditType  IN ('L', 'LinkedServer') BEGIN
     
      DELETE FROM [audit].[Setting] WHERE [ID] in( 1, 2, 3)
      INSERT [audit].[Setting] ([ID], [IntValue], [Code], [StrValue])
      VALUES (1,2,'AuditSPTypeDefault','LinkedServer'),
             (2,2,'AuditLTTypeDefault','LinkedServer'),
             (3,2,'AuditErrTypeDefault','LinkedServer')

      EXEC [audit].[sp_Print] 'Instaled Audit Type: LinkedServer'
  END
  IF @IsEnable = 1 AND @AuditType IN ('T', 'Table') OR NOT EXISTS (SELECT * FROM [audit].[Setting] WHERE  [ID] in ( 1, 2, 3) ) BEGIN
      DELETE FROM [audit].[Setting] WHERE [ID] in( 1, 2, 3)
      INSERT [audit].[Setting] ([ID], [IntValue], [Code], [StrValue])
      VALUES (1,1,'AuditSPTypeDefault','Table'),
             (2,1,'AuditLTTypeDefault','Table'),
             (3,1,'AuditErrTypeDefault','Table')
      
      EXEC [audit].[sp_Print] 'Instaled Audit Type: Table'
  END

END