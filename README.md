# Injekko
<p align="center">
  <img src="https://github.com/user-attachments/assets/791c858c-5234-4775-a166-62909747d7db">
</p>

**Injekko** is a dependency injection framework for Unity that avoids reflection entirely. Instead, it leverages **Roslyn** to analyze your code as you write it and automatically generate strongly-typed resolvers, compiled into a dynamic DLL. This results in **fast, safe, and highly efficient** injection, ideal for performance-critical environments like game development.

> 🚧 **Work in progress**: Injekko is still under active development. Core features are being implemented incrementally as part of a long-term vision.

## Features

- 🔧 Zero reflection – fully based on code generation.
- ⚡ Auto-generated resolvers at compile time using Roslyn.
- 🎮 Designed to integrate seamlessly with Unity workflows.
- 🧩 Supports a Service Locator pattern with multiple contexts (global, scene, object).
- 🧪 Includes a playground project to test generated bindings.
- 🔜 **Planned**: Future versions aim to be **engine-agnostic**, making Injekko usable beyond Unity.

