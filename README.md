# SAMI: SNMP to SSAM Interface Service

# About

SAMI is a Windows service that provides a junction between SNMP traps and database operations. Database operations default to sending SSAM events triggered through database stored procedure, but database commands are fully customizable.

## Requirements
* 64-bit Windows 10 / Windows 2016 Server or newer.
* .NET 4.6 or newer.
* Database management system such as:
  * SQL Server (Recommended)
  * MySQL
  * Oracle
  * PostgreSQL
  * SQLite (Not recommended for production use) - included.

  ---

  This project was created with the Grid Solutions Framework Time-Series Library [Project Alpha](https://github.com/GridProtectionAlliance/projectalpha)

  [![Project Alpha](https://github.com/GridProtectionAlliance/PTPSync/raw/master/Source/Documentation/Images/Project-Alpha-Logo_70.png)](https://github.com/GridProtectionAlliance/projectalpha)
