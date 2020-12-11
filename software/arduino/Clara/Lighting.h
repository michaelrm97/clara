#ifndef LIGHTING_H_
#define LIGHTING_H_

#include "LightingConfiguration.h"

void setupLights();

void applyCommand(Command command);
void showLights(uint16_t brightness);
void resetLights();

#endif /* LIGHTING_H_ */
