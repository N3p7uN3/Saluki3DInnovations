#ifndef _VSARDUINO_H_
#define _VSARDUINO_H_
//Board = Arduino Mega or Mega 2560 w/ ATmega2560 (Mega 2560)
#define __AVR_ATmega2560__
#define 
#define ARDUINO 155
#define ARDUINO_MAIN
#define __AVR__
#define F_CPU 16000000L
#define __cplusplus
#define __inline__
#define __asm__(x)
#define __extension__
#define __ATTR_PURE__
#define __ATTR_CONST__
#define __inline__
#define __asm__ 
#define __volatile__

#define __builtin_va_list
#define __builtin_va_start
#define __builtin_va_end
#define __DOXYGEN__
#define __attribute__(x)
#define NOINLINE __attribute__((noinline))
#define prog_void
#define PGM_VOID_P int
            
typedef unsigned char byte;
extern "C" void __cxa_pure_virtual() {;}

//
//
void processRawPacket();
void processPacket();
void processCommand();
void notifyFailure(long commandNum);
void sendPacket(String packet);
int calcChecksum(String cmd);
int freeRam ();
void sendOkayComplete();
void sendOkay();
void sendComplete();

#include "C:\Program Files (x86)\Arduino\hardware\arduino\avr\variants\mega\pins_arduino.h" 
#include "C:\Program Files (x86)\Arduino\hardware\arduino\avr\cores\arduino\arduino.h"
#include "c:\Users\Wheeler\Downloads\Saluki3DInnovations 4-22 1550\Saluki3DInnovations\MicrocontrollerSoftware\MicrocontrollerSoftware.ino"
#endif
