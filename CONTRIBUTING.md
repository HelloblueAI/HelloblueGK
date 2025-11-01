# Contributing to HelloblueGK

Thank you for your interest in contributing to HelloblueGK! This document provides guidelines and instructions for contributing.

## Code of Conduct

We are committed to fostering a welcoming and inclusive community. Please be respectful and professional in all interactions.

## How to Contribute

### Reporting Issues

If you find a bug or have a feature request:

1. Check if the issue already exists in the [Issues](https://github.com/HelloblueAI/HelloblueGK/issues) section
2. If not, create a new issue with:
   - Clear title and description
   - Steps to reproduce (for bugs)
   - Expected vs actual behavior
   - Environment details (.NET version, OS, etc.)

### Contributing Code

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/your-feature-name`
3. **Make your changes**:
   - Follow existing code style
   - Add tests for new features
   - Update documentation as needed
   - Ensure all tests pass
4. **Commit your changes**: Use clear, descriptive commit messages
5. **Push to your fork**: `git push origin feature/your-feature-name`
6. **Create a Pull Request**:
   - Provide a clear description of changes
   - Reference any related issues
   - Ensure CI/CD checks pass

## Development Setup

### Prerequisites

- .NET 9.0 SDK
- Docker (optional, for containerized development)
- Git

### Getting Started

1. Clone the repository:
```bash
git clone https://github.com/HelloblueAI/HelloblueGK.git
cd HelloblueGK
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the solution:
```bash
dotnet build
```

4. Run tests:
```bash
dotnet test
```

5. Run the application:
```bash
dotnet run
```

### Running the Web API Demo

See [DEMO.md](DEMO.md) for instructions on running the interactive demo.

## Code Style Guidelines

- Follow C# coding conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and small
- Write unit tests for new features
- Ensure code coverage remains above 90%

## Testing

- Write unit tests for new features
- Ensure integration tests pass
- Run performance benchmarks if applicable
- Verify all CI/CD checks pass

## Documentation

- Update README.md if adding new features
- Add XML documentation for public APIs
- Update API documentation if endpoints change
- Keep technical documentation up to date

## Pull Request Process

1. Ensure your branch is up to date with `main`
2. All tests must pass
3. Code must follow style guidelines
4. CI/CD checks must pass
5. PR description should clearly explain changes
6. Request review from maintainers

## License

By contributing, you agree that your contributions will be licensed under the Apache License 2.0.

## Questions?

Feel free to open an issue or reach out to the maintainers if you have questions about contributing.

Thank you for contributing to HelloblueGK! 🚀

