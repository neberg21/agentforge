## Conventions
- Use NSubstitute for mocking
- Use xUnit v3 for unit tests

### Naming Pattern
Test libraries should be named <Project>.<TestType>, eg `AgentForge.Core.Unit`

- Unit: MethodName_Scenario_Expectation (e.g. CreateAsync_WhenNameTaken_ReturnsConflict)
- Integration: ResourceName_Scenario_Expectation (e.g. AgentDefinitions_WhenUnknown_ReturnsNotFoundProblemDetails)
- Architecture: RuleName_Scenario_Expectation (e.g. AreaIsolation_WhenReferencingHost_IsForbidden)
