# Project Definition Questionnaire

> Fill in the answers below to produce an unambiguous project prompt that any developer or LLM can pick up and execute without gaps.

---

## Identity & Purpose

1. What is the app called?
   > 

2. In one sentence, what does it do?
   > 

3. Who is the primary user? (developer tool, consumer app, internal business tool, etc.)
   > 

4. What problem does it solve, and what's the current painful alternative?
   > 

---

## Platform & Runtime

5. Target platform(s): Windows only, Mac, Linux, cross-platform, web, mobile?
   > 

6. What language? (C#, TypeScript, Python, Rust, etc.)
   > 

7. What runtime/version? (.NET 8, Node 20, Python 3.12, etc.)
   > 

8. What UI framework? (Avalonia, WPF, React, MAUI, Electron, etc.)
   > 

9. What architectural pattern? (MVVM, MVC, clean architecture, feature-slice, etc.)
   > 

10. Single executable / installer / web app / background service?
    > 

---

## Project Structure

11. Monorepo or separate repos?
    > 

12. What are the top-level projects/assemblies? (e.g. Core, UI, API, Tests)
    > 

13. What namespace convention? (e.g. `CompanyName.AppName.Feature`)
    > 

14. Folder naming convention: PascalCase, kebab-case, snake_case?
    > 

15. Where do tests live? Same project, separate test project, or side-by-side?
    > 

---

## Data & State

16. What data does the app create, read, update, or delete?
    > 

17. Where does data live? (local file, SQLite, remote API, cloud DB, in-memory only)
    > 

18. What is the file format if saving locally? (JSON, XML, SQLite, binary, custom)
    > 

19. Does state persist between sessions or reset on launch?
    > 

20. Is there any user account / authentication involved?
    > 

---

## Core Features

21. List every feature the app must have at launch (v1.0). Be exhaustive.
    > 

22. List features that are explicitly OUT OF SCOPE for v1.0.
    > 

23. What is the single most important feature — the one the app cannot exist without?
    > 

24. Are there any features that depend on each other? (e.g. Feature B requires Feature A first)
    > 

---

## UI & UX

25. Describe the main screen/view. What does the user see first?
    > 

26. How many distinct screens/views/pages are there?
    > 

27. List them and describe the purpose of each.
    > 

28. What is the primary user interaction? (click, drag, keyboard shortcut, form fill, etc.)
    > 

29. Is there a specific visual style or theme? (dark/light mode, color palette, font preferences)
    > 

30. Are there any UI components that need custom rendering? (custom controls, canvas drawing, etc.)
    > 

31. Does the app need to be accessible? (screen reader support, keyboard navigation, contrast ratios)
    > 

---

## Communication & Events

32. How do different parts of the app talk to each other? (events, message bus, direct calls, DI)
    > 

33. Are there background threads or async operations? What triggers them?
    > 

34. Does the app call any external APIs or services? If so, which ones and what data do they return?
    > 

35. Does the app need real-time updates? (polling, websockets, file watchers, etc.)
    > 

---

## Error Handling & Logging

36. What should happen when something goes wrong? (silent fail, user notification, crash report)
    > 

37. Is there a logging requirement? (console, file, remote telemetry)
    > 

38. What errors are recoverable vs. fatal?
    > 

---

## Performance & Constraints

39. Any performance targets? (startup time, frame rate, max memory, file size limits)
    > 

40. Expected data volume? (10 items, 10,000 items, millions of records)
    > 

41. Any offline requirement?
    > 

42. Any security or privacy constraints? (no network calls, encrypted storage, no telemetry)
    > 

---

## Testing

43. What level of test coverage is expected? (unit, integration, end-to-end, or none for now)
    > 

44. What test framework? (xUnit, NUnit, Jest, pytest, etc.)
    > 

45. Are there any flows that absolutely must have tests before shipping?
    > 

---

## Build & Distribution

46. How is the app built? (CLI, IDE, CI pipeline)
    > 

47. How is it distributed? (GitHub release, installer, NuGet, npm, internal only)
    > 

48. Is there a versioning scheme? (SemVer, date-based, etc.)
    > 

49. Any signing or notarization requirements?
    > 

---

## Dependencies & Third-Party

50. What NuGet/npm/pip packages are already decided on?
    > 

51. Any packages that are explicitly banned or unwanted?
    > 

52. Any licensing constraints on dependencies?
    > 

---

## Team & Process

53. How many developers will work on this?
    > 

54. What is the branching strategy? (main/staging, trunk-based, gitflow)
    > 

55. Any coding style rules? (tabs vs spaces, max line length, naming conventions, etc.)
    > 

56. Where does the project live? (GitHub, GitLab, Azure DevOps, local only)
    > 

57. Will LLMs be contributing code? If so, what branch do they push to?
    > 

---

*Once all questions are answered, this document can be used as the canonical prompt for any developer or AI assistant joining the project.*
