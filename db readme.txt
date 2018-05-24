
//////////////////////////////////////
in SQLserver Management Studio 
//////////////////////////////////////

new a database,name: basisagent

restore the database from file: basisagent_empty.bak 

pay attention ,tick off "Overwrite the existing database".


the host info please open the "host_command" table , manual filling.


///////////////////////////
about connect the SQLserver
///////////////////////////

"App.config" in Project,modify the content:

data source=C08243;initial catalog=basisagent;user id=sa;password=123456789
