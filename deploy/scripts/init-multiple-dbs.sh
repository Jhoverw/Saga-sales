OBSOLETO — este script era la inicialización de PostgreSQL (creaba varias bases al arrancar el
contenedor). La plataforma usa SQL Server, que no tiene un directorio de init scripts equivalente.

Las bases de datos las crea `scripts/apply-db.ps1` en su primera ejecución
(`IF DB_ID('saga_<servicio>') IS NULL CREATE DATABASE ...`), y el esquema lo aplican los scripts
versionados de `db/<servicio>/`.

Puedes borrar este archivo.
