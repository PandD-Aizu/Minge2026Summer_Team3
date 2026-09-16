# agents.md

## Code Review Strategies

- Explain in Japanese 

- For programming beginners, make sure to carefully explain which points are problematic and why 
  - However, the following users are experienced, so point out even the minor details 
    - Aya-8 
    - koto-thing

- Check the following points
  1. Bugs or behavior that may break the game
  2. Crashes, null references, invalid memory access, or undefined behavior
  3. Incorrect game state transitions
  4. Resource or lifecycle problems
  5. Performance problems that may noticeably affect gameplay
  6. Maintainability problems that are likely to cause future bugs
  7. Readability problems
  8. Minor style issues

- Ignore the following points
  - purely subjective style preferences
  - formatting issues handled automatically by tooling
  - speculative problems with no realistic failure scenario
  - large architectural rewrites unrelated to the pull request
  - issues in unchanged code unless the pull request makes them significantly worse

## Comment Strategies

- No period at the end of a sentence
- No need to use polite forms (desu/masu)
- Add comments to functions briefly explaining what the function does, the meaning of its arguments and return values, and usage examples
- Add appropriate comments for each block of processing
- Insert appropriate blank lines for each block of processing