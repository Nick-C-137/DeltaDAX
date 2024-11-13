WITH

base as (
    SELECT
         [_BK - Shippers Bridge Example] = ShipperID
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