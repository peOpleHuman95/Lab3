// Разрешаем независимым тестовым методам выполняться параллельно.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
