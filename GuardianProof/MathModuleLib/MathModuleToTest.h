#pragma once

#ifdef MATHMODULELIB_EXPORTS
#define MATHMODULE_API __declspec(dllexport)
#else
#define MATHMODULE_API __declspec(dllimport)
#endif

class MATHMODULE_API MathModule {

public:
	static int add(int a, int b);

	static int subtract(int a, int b);

	static int multiply(int a, int b);

	static int divide(int a, int b);

	static int divideModulo(int a, int b);
};
