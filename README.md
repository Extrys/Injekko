# Injekko
<p align="center">
  <img src="https://github.com/user-attachments/assets/791c858c-5234-4775-a166-62909747d7db">
</p>

**Injekko** is a Unity-first dependency injection framework that avoids reflection entirely. Instead, it leverages **Roslyn** to analyze your code as you write it and automatically generate strongly-typed resolvers and Fucktories. This results in **fast, safe, and highly efficient** injection, ideal for performance-critical environments like game development.

> Work in progress: Injekko is still under active development. Core features are being implemented incrementally as part of a long-term Unity-focused vision.

## Features

- Zero reflection, fully based on code generation.
- Auto-generated resolvers and Fucktories at compile time using Roslyn.
- Unity-first scope tree model with generated activation.
- Playground project to validate bindings and creation flows.
- Metadata groundwork for future Graph Toolkit tooling.
