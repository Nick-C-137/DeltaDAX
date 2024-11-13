DROP VIEW IF EXISTS dwh.[~ BRI - Shippers Bridge Example to Customers]
GO 

CREATE VIEW dwh.[~ BRI - Shippers Bridge Example to Customers] AS
WITH

customer_table as (
    SELECT
        [_BK - Customers] = CAST(CustomerID AS NVARCHAR(100))
    FROM
        dbo.Customers
),

shipper_table_2 as (
    SELECT
        [_BK - Shippers] = '2'

),

shipper_table_3 as (
    SELECT
        [_BK - Shippers] = '3'

),

cross_joined_2 as (
    SELECT
        *
    FROM
        customer_table
    CROSS JOIN
        shipper_table_2
),


cross_joined_3 as (
    SELECT
        *
    FROM
        customer_table
    CROSS JOIN
        shipper_table_3
),

unioned as (
    SELECT * FROM cross_joined_2 UNION ALL
    SELECT * FROM cross_joined_3
)

SELECT
     [_BK - Customers] = [_BK - Customers]
    ,[_BK - Shippers Bridge Example] = [_BK - Shippers]
    ,last_updated = GETDATE()
FROM
    unioned



