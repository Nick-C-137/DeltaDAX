CREATE VIEW dwh.[~ DIM Shippers] AS
WITH

base as (
    SELECT
         [_BK - Shippers] = ShipperID
        ,[ShipperID]
        ,[CompanyName]
        ,[Phone]
    FROM
        dbo.Shippers
)

SELECT
    *
FROM
    base