-- El usuario de las pruebas solo puede crear, usar y borrar bases cuyo nombre empieza con control_test_.
GRANT ALL PRIVILEGES ON `control\_test\_%`.* TO 'pruebas'@'%';
FLUSH PRIVILEGES;
