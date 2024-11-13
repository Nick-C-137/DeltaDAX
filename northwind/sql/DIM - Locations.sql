CREATE VIEW dwh.[~ DIM - Locations] AS
WITH

base as (
    SELECT DISTINCT
        [_BK - Locations]   = CONCAT(
                                         ShipAddress
                                        ,ShipCity
                                        ,ShipRegion
                                        ,ShipPostalCode
                                        ,ShipCountry
                                    )
       ,[Ship Address]              = ShipAddress
       ,[Ship City]                 = ShipCity
       ,[Ship Region]               = ShipRegion
       ,[Ship Postal Code]          = ShipPostalCode
       ,[Ship Country]              = ShipCountry
    FROM
        dbo.Orders
)

SELECT
    *
FROM
    base



