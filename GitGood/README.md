# GitGood

A smart Git commit assistant powered by AI that helps you write better commit messages.

## Features

- AI-powered commit message generation
- Integration with OpenAI's GPT models
- GitHub integration for repository context
- Interactive CLI interface
- Local Git operations support

## Installation

Install GitGood globally using the .NET CLI:

```bash
dotnet tool install -g gitgood
```

## Configuration

Before using GitGood, you need to configure your OpenAI API key and GitHub Personal Access Token:

```bash
gitgood config
```

This will prompt you to enter:
- OpenAI API key
- OpenAI Chat Model ID (default: gpt-4o)
- OpenAI Reasoning Effort (default: medium)
- GitHub Personal Access Token

## Usage

### Interactive Mode

Simply run:

```bash
gitgood
```

This will start an interactive session where you can ask questions about your Git operations.

### Commit Mode

To generate a commit message for your changes:

```bash
gitgood commit <OrgName>
```

## License

This project is licensed under the MIT License - see the LICENSE file for details. 