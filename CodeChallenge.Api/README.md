Question1 : Describe your implementation approach and the key decisions you made.

Answer : I separated controllers, logic, and repository so each layer has a clear responsibility.
All business rules were implemented in MessageLogic, keeping controllers thin and easy to maintain.

Question2 : What would you improve or change if you had more time?

Answer : I would add proper validation libraries, better error handling, and replace the in-memory repo with a real database and also include pagination , mapping layers

Question3 : How did you approach the validation requirements and why?

Answer : I centralized all validation inside the logic layer to avoid duplication and keep rules consistent.
This also makes the logic easier to test independently from the API.

Question4 : What changes would you make to this implementation for a production environment?

Answer : I will use a real data store, add authentication/authorization, and improve error handling and observability.

Question5 : Explain your testing strategy and the tools you chose.

Answer : I mocked the repository and focused on business rules so tests stay fast and isolated.
xUnit, Moq, and FluentAssertions made the tests simple.

Question6 : What other scenarios would you test in a real-world application?

Answer : I would test performance, security, and full integration of API + DB.
Also edge cases like large payloads, invalid JSON, and authorization failures.