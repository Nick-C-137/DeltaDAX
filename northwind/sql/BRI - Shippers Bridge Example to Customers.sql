WITH

customer_table as (
    SELECT
        [_BK - Customers] = CustomerID
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
FROM
    unioned



