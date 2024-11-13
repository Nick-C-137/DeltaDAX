DROP VIEW IF EXISTS dwh.[~ DIM Shippers] 
GO

CREATE VIEW dwh.[~ DIM Shippers] AS
WITH

base as (
    SELECT
         [_BK - Shippers] = CAST(ShipperID AS NVARCHAR(100))
        ,[ShipperID]
        ,[CompanyName]
        ,[Phone]
        ,last_updated = GETDATE()
    FROM
        dbo.Shippers
)

SELECT
    *
FROM
    base