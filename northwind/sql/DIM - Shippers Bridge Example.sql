 DROP VIEW IF EXISTS dwh.[~ DIM - Shippers Bridge Example]
 GO 

CREATE VIEW dwh.[~ DIM - Shippers Bridge Example] AS
WITH

base as (
    SELECT
         [_BK - Shippers Bridge Example] = CAST(ShipperID AS NVARCHAR(100))
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