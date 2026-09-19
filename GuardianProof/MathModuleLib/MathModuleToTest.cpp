#include "MathModuleToTest.h"


int MathModule::add(int a, int b) {
	return a + b;
}

int MathModule::subtract(int a, int b) {
	return a - b;
}

int MathModule::multiply(int a, int b) {
	return a * b;
}

int MathModule::divide(int a, int b) {
	return b == 0 ? 0 : a / b;
}

int MathModule::divideModulo(int a, int b) {
	//Beabsichtigte falsche Implementierung
	return a + b;
}
