/*
  Like tone except it makes use of the DAC at the expense of not working for every pin
 */

#include "Arduino.h"
#include "variant.h"

#include "MyTone.h"

#define WAIT_TC16_REGS_SYNC(x) while(x->COUNT16.STATUS.bit.SYNCBUSY);

uint32_t myToneMaxFrequency = F_CPU / 2;

volatile int64_t myToggleCount;
volatile bool myToneIsActive = false;
volatile bool myFirstTimeRunning = false;

#define N_SEGMENTS 2

#define TONE_TC         TC5
#define TONE_TC_IRQn    TC5_IRQn
#define TONE_TC_TOP     0xFFFF
#define TONE_TC_CHANNEL 0

#define TONE_OUTPUT_PIN A0

#define OUTPUT_MIDPOINT 128
#define MAX_AMPLITUDE   128
#define MAX_OUTPUT      255

uint16_t segmentNum;
uint16_t outputValues[N_SEGMENTS];

constexpr float sineValues(int n) {
  return sin(-PI/2 + 2 * PI * n / N_SEGMENTS);
}

void TC5_Handler (void) __attribute__ ((weak, alias("My_Tone_Handler")));

static inline void resetTC (Tc* TCx)
{
  // Disable TCx
  TCx->COUNT16.CTRLA.reg &= ~TC_CTRLA_ENABLE;
  WAIT_TC16_REGS_SYNC(TCx)

  // Reset TCx
  TCx->COUNT16.CTRLA.reg = TC_CTRLA_SWRST;
  WAIT_TC16_REGS_SYNC(TCx)
  while (TCx->COUNT16.CTRLA.bit.SWRST);
}

void myTone (unsigned int frequency, unsigned int amplitude, unsigned long duration)
{
  // Avoid divide by zero error by calling 'noTone' instead
  if (frequency == 0 || amplitude == 0)
  {
    myNoTone();
    return;
  }

  if (amplitude > MAX_AMPLITUDE) {
    amplitude = MAX_AMPLITUDE;
  }

  // Multiply frequency by N_SEGMENTS / 2
  frequency = frequency * N_SEGMENTS / 2;
  
  // Configure interrupt request
  NVIC_DisableIRQ(TONE_TC_IRQn);
  NVIC_ClearPendingIRQ(TONE_TC_IRQn);
  
  if(!myFirstTimeRunning)
  {
    myFirstTimeRunning = true;
    
    NVIC_SetPriority(TONE_TC_IRQn, 0);
      
    // Enable GCLK for TC4 and TC5 (timer counter input clock)
    GCLK->CLKCTRL.reg = (uint16_t) (GCLK_CLKCTRL_CLKEN | GCLK_CLKCTRL_GEN_GCLK0 | GCLK_CLKCTRL_ID(GCM_TC4_TC5));
    while (GCLK->STATUS.bit.SYNCBUSY);
  }
  
  if (myToneIsActive) {
    myNoTone();
  }

  //
  // Calculate best prescaler divider and comparator value for a 16 bit TC peripheral
  //

  uint32_t prescalerConfigBits;
  uint32_t ccValue;

  ccValue = myToneMaxFrequency / frequency - 1;
  prescalerConfigBits = TC_CTRLA_PRESCALER_DIV1;
  
  uint8_t i = 0;
  
  while(ccValue > TONE_TC_TOP)
  {
    ccValue = myToneMaxFrequency / frequency / (2<<i) - 1;
    i++;
    if(i == 4 || i == 6 || i == 8) //DIV32 DIV128 and DIV512 are not available
     i++;
  }
  
  switch(i-1)
  {
    case 0: prescalerConfigBits = TC_CTRLA_PRESCALER_DIV2; break;
    
    case 1: prescalerConfigBits = TC_CTRLA_PRESCALER_DIV4; break;
    
    case 2: prescalerConfigBits = TC_CTRLA_PRESCALER_DIV8; break;
    
    case 3: prescalerConfigBits = TC_CTRLA_PRESCALER_DIV16; break;
    
    case 5: prescalerConfigBits = TC_CTRLA_PRESCALER_DIV64; break;
      
    case 7: prescalerConfigBits = TC_CTRLA_PRESCALER_DIV256; break;
    
    case 9: prescalerConfigBits = TC_CTRLA_PRESCALER_DIV1024; break;
    
    default: break;
  }

  myToggleCount = (duration > 0 ? frequency * duration * 2 / 1000UL : -1LL);

  resetTC(TONE_TC);

  uint16_t tmpReg = 0;
  tmpReg |= TC_CTRLA_MODE_COUNT16;  // Set Timer counter Mode to 16 bits
  tmpReg |= TC_CTRLA_WAVEGEN_MFRQ;  // Set TONE_TC mode as match frequency
  tmpReg |= prescalerConfigBits;
  TONE_TC->COUNT16.CTRLA.reg |= tmpReg;
  WAIT_TC16_REGS_SYNC(TONE_TC)

  TONE_TC->COUNT16.CC[TONE_TC_CHANNEL].reg = (uint16_t) ccValue;
  WAIT_TC16_REGS_SYNC(TONE_TC)

  // Enable the TONE_TC interrupt request
  TONE_TC->COUNT16.INTENSET.bit.MC0 = 1;
  
  myToneIsActive = true;
  segmentNum = 0;

  for (int i = 0; i < N_SEGMENTS; i++) {
    outputValues[i] = OUTPUT_MIDPOINT + sineValues(i) * amplitude;
    if (outputValues[i] > MAX_OUTPUT) {
      outputValues[i] = MAX_OUTPUT;
    }
  }

  // Enable TONE_TC
  TONE_TC->COUNT16.CTRLA.reg |= TC_CTRLA_ENABLE;
  WAIT_TC16_REGS_SYNC(TONE_TC)
  
  NVIC_EnableIRQ(TONE_TC_IRQn);
}

void myNoTone ()
{
  /* 'tone' need to run at least once in order to enable GCLK for
   * the timers used for the tone-functionality. If 'noTone' is called
   * without ever calling 'tone' before then 'WAIT_TC16_REGS_SYNC(TCx)'
   * will wait infinitely. The variable 'myFirstTimeRunning' is set the
   * 1st time 'tone' is set so it can be used to detect wether or not
   * 'tone' has been called before.
   */
  if(myFirstTimeRunning)
  {
    resetTC(TONE_TC);
    analogWrite(TONE_OUTPUT_PIN, OUTPUT_MIDPOINT);
    myToneIsActive = false;
  }
}

#ifdef __cplusplus
extern "C" {
#endif

void My_Tone_Handler (void)
{
  if (myToggleCount != 0)
  {
    // Set value
    analogWrite(TONE_OUTPUT_PIN, outputValues[segmentNum]);
    segmentNum++;
    if (segmentNum >= N_SEGMENTS) {
      segmentNum = 0;
    }

    if (myToggleCount > 0)
      --myToggleCount;

    // Clear the interrupt
    TONE_TC->COUNT16.INTFLAG.bit.MC0 = 1;
  }
  else
  {
    resetTC(TONE_TC);
    myToneIsActive = false;
  }
}

#ifdef __cplusplus
}
#endif
