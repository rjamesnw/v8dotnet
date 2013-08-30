// Get target CPU architecture.
// Windows: _WIN64 or _WIN32
// GNUC: __x86_64__ or __ppc64__
#if _WIN64 || __x86_64__ || __ppc64__ || INTPTR_MAX == INT64_MAX
#define ENV64
#else
#define ENV32 // (default to 32-bit if 64-bit cannot be detected)
#endif
