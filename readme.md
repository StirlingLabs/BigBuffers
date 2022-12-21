![BigBuffers](./docs/images/bigbuffers.jpg) 

BigBuffers has been deprecated and is replaced internally by [CloudBuffers](https://github.com/StirlingLabs/CloudBufffers) and [Unlocal](https://github.com/StirlingLabs/Unlocal), which we expect to open source.

**BigBuffers** is a cross platform serialization library architected for maximum memory efficiency and large data sets. 

**BigBuffers** is a fork of **FlatBuffers** with 64 bit indexes, to supports buffers >2Gb.  Both projects allow you to directly access serialized data without parsing/unpacking it first, while still having great forwards/backwards compatibility... but they are not wire-compatible. 

Browse our [docs directory](docs) for more details, including [differences](docs/source/DifferencesFromFlat.md).

## Supported operating systems

Primary platforms (in use by maintainers):

* Windows
* MacOS
* Linux

Secondary platforms (maintainers welcome):

* Android
* And any others with a recent C++ compiler.

## Supported programming languages

* C#
* C (use [our fork of flatcc](https://github.com/StirlingLabs/flatcc) in 64-bit mode)

## Nearly supported languages

So far we are only using C & C# so have concentrated on modernising these language implementations and have removed unsupported languages from the repo.  If you are interested in upgrading the FlatBuffers code to add BigBuffers support for any other languages, please create a PR here.  FlatBuffers provides a good starting point for these languages and we would welcome them back into the repo:

* C++
* Go
* Python
* Rust
* JavaScript
* TypeScript
* Dart
* Java
* Lobster
* Lua
* PHP

## Contributing to BigBuffers
Submit an issue or PR as per our [CONTRIBUTING](CONTRIBUTING.md) page.

## Security
Please see the [Security Policy](SECURITY.md) for reporting vulnerabilities.

## Licensing
*BigBuffers*, like *FlatBuffers*, is licensed under the Apache License, Version 2.0.
See [LICENSE.txt](LICENSE.txt) for the full license text.

![I like bug buffers](./docs/images/i-like-big-buffers-and-i-cannot-lie.jpg) 
