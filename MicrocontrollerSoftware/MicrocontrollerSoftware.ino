#include <AccelStepper.h>
#include <InkShieldMega.h>

unsigned long serialBaud = 57600;
String packetBuffer;
String payload;
int val;
char endLineChar = 126;

//AccelStepper(stepperMotorType, stepPin, directionPin)
AccelStepper xStepper(1, 52, 53);
long xTranslatingTo;
bool xMoving;

AccelStepper platformStepper(1, 50, 51);
long platformTranslatingTo;
bool platformMoving;

AccelStepper sourceStepper(1, 48, 49);
long sourceTranslatingTo;
bool sourceMoving;

AccelStepper yStepperA(1, 22, 23);
AccelStepper yStepperB(1, 20, 21);
long yTranslatingTo;
bool yMoving;

AccelStepper rollingStepper(1, 24, 25);
long rollingTranslateTo;
bool rollingMoving;



bool masterRunning;	//emergency stop functionality

word printArray[2000];
int printLocation;	//current location within the print array
int length;			//amount of dot arrays ready to be printed, we will decrement this for every array that is fired
byte xDotDensity;	//steps in between dot patterns
long nextFireLocation;	//location in X axis of the next nozzle fire
bool printing;
bool printingDirection;	//true for negative direction.

InkShieldA0A3 MyInkShield(2);

//testing functionality
bool firingContinuously;
unsigned long prevMillis;

int testpoint[10];

int xTransSpeed, xTransAccel, xPrintSpeed, xPrintAccel;


void setup()
{
	Serial.begin(serialBaud);
	//Serial.flush();
	packetBuffer.reserve(64);
	payload.reserve(48);
	

	masterRunning = true;

	pinMode(13, OUTPUT);
	digitalWrite(13, LOW);

	packetBuffer = "";

	//setup stepper motors
	xTransSpeed = 650;
	xTransAccel = 3000;
	xPrintSpeed = 200;
	xPrintAccel = 500;
	xStepper.setCurrentPosition(0);
	xTranslatingTo = 0;
	xMoving = false;

	yStepperA.setMaxSpeed(650);
	yStepperA.setAcceleration(1250);
	yStepperA.setCurrentPosition(0);
	yStepperB.setMaxSpeed(650);
	yStepperB.setAcceleration(1250);
	yStepperB.setCurrentPosition(0);
	yStepperA.setPinsInverted(false, false, false);
	yStepperB.setPinsInverted(false, false, false);
	yTranslatingTo = 0;
	yMoving = false;

	platformStepper.setMaxSpeed(100);
	platformStepper.setAcceleration(250);
	platformStepper.setCurrentPosition(0);
	platformTranslatingTo = 0;
	platformMoving = false;

	sourceStepper.setMaxSpeed(100);
	sourceStepper.setAcceleration(250);
	sourceStepper.setCurrentPosition(0);
	sourceTranslatingTo = 0;
	sourceMoving = false;

	rollingStepper.setMaxSpeed(650);
	rollingStepper.setAcceleration(1000);
	rollingStepper.setCurrentPosition(0);
	rollingTranslateTo = 0;
	rollingMoving = false;

	xDotDensity = 14;

	firingContinuously = false;
}

void loop()
{
	
	//Serial read stuff
	if (Serial.available() > 0)
	{
		val = Serial.read();

		if ((val == (int)endLineChar) && (packetBuffer.length() > 0))
		{

			processRawPacket();

			packetBuffer = "";
		}
		else
		{
			
			packetBuffer = packetBuffer + (char)val;
		}
		int dummy = freeRam();
		if (testpoint[0] < dummy)
			testpoint[0] = dummy;
	}

	if (xMoving)
	{
		if ((printing) && (masterRunning))
		{
			if (xStepper.currentPosition() == nextFireLocation)
			{
				MyInkShield.spray_ink(printArray[printLocation]);
				//String strFirePattern = String(printArray[printLocation]);
				++printLocation;
				if (printingDirection)
					nextFireLocation = nextFireLocation - xDotDensity;
				else
					nextFireLocation = nextFireLocation + xDotDensity;

				if (printLocation > length)	//the current location that we want to print is out of range, therefore, done printing
					printing = false;
			}
		}

		if (xStepper.currentPosition() == xTranslatingTo)
		{
			//finished moving
			xMoving = false;
			sendComplete();
		}
	}

	if (platformMoving)
	{
		if (platformStepper.currentPosition() == platformTranslatingTo)
		{
			platformMoving = false;
			sendComplete();
		}
	}

	if (sourceMoving)
	{
		if (sourceStepper.currentPosition() == sourceTranslatingTo)
		{
			sourceMoving = false;
			sendComplete();
		}
	}

	if (yMoving)
	{
		if (yStepperA.currentPosition() == yTranslatingTo)
		{
			yMoving = false;
			sendComplete();

		}
	}

	if (rollingMoving)
	{
		if (rollingStepper.currentPosition() == rollingTranslateTo)
		{
			rollingMoving = false;
			sendComplete();
		}
	}

	if ((firingContinuously) && (masterRunning))
	{
		unsigned long curMillis = millis();
		if ((curMillis - prevMillis) > 10)
		{
			prevMillis = curMillis;
			MyInkShield.spray_ink(0x0FFF);
		}
		
		delay(1);
	}

	//EMERGENCY STOP FUNCTIONALITY
	//Also the functionality that enables motors to move.
	//We want this called AFTER the function that prints the lines, such that, if the first array is to be fired at
	//the current location, the first array does indeed fire before the X axis begins translating.
	if (masterRunning)
	{
		xStepper.run();
		platformStepper.run();
		sourceStepper.run();
		yStepperA.run();
		yStepperB.run();
		rollingStepper.run();
	}

	int dummy8 = freeRam();
	if (dummy8 > testpoint[8])
		testpoint[8] = dummy8;

}

void processRawPacket()
{
	//hello?eee
	//packetBuffer = packetBuffer.substring(0, packetBuffer.length() - 1);
	
	if (packetBuffer == F("hello?"))
	{
		//handshake.
		sendPacket(F("hello."));
	}
	else if (packetBuffer == F("sos"))
	{
		masterRunning = false;
	} else
	{
		//Packet has header and footer, process accordingly.
		processPacket();
		int dummy = freeRam();
		if (testpoint[1] < dummy)
			testpoint[1] = dummy;
	}
}

void processPacket()
{
	String strLineNum, strCheckSum;
	
	strLineNum = packetBuffer.substring(0, 11);
	long lineNum = strLineNum.toInt();
	//0000000000payloadpayload00000
	int locationOfChecksum = packetBuffer.length() - 5;
	strCheckSum = packetBuffer.substring(locationOfChecksum);

	int checksum = strCheckSum.toInt();

	payload = packetBuffer.substring(10, locationOfChecksum);
	int dummy = freeRam();
		if (testpoint[2] < dummy)
			testpoint[2] = dummy;
	int calculatedChecksum = calcChecksum(payload);

	int dummy3 = freeRam();
		if (testpoint[3] < dummy3)
			testpoint[3] = dummy3;

	if (checksum == calculatedChecksum)
	{
		//packet is good, process the command
		processCommand();
	}
	else
	{
		//packet is bad, notify the computer
		notifyFailure(lineNum);
	}

}

void processCommand()
{
	int firstSpace = payload.indexOf(' ');
	int dummy = freeRam();
		if (testpoint[4] < dummy)
			testpoint[4] = dummy;
	if (firstSpace > -1)
	{
		//command has a space in it, parse for the first word in the command

		String firstWord = payload.substring(0, firstSpace);

		long pos;

		
		if (firstWord == F("t4"))	//translate X NOT printing
		{
			//t4 00000000

			xStepper.setMaxSpeed(xTransSpeed);
			xStepper.setAcceleration(xTransAccel);

			pos = payload.substring(3).toInt();
			xStepper.moveTo(pos);
			xTranslatingTo = pos;
			xMoving = true;

			sendOkay();
		}

		if (firstWord == F("t5"))
		{
			pos = payload.substring(3).toInt();
			yStepperA.moveTo(pos);
			yStepperB.moveTo(-pos);
			yTranslatingTo = pos;
			yMoving = true;
			
			sendOkay();
		}

		if (firstWord == F("t2"))	//translate powder bed platform
		{
			pos = payload.substring(3).toInt();
			platformStepper.moveTo(pos);
			platformTranslatingTo = pos;
			platformMoving = true;

			
			sendOkay();

		}

		if (firstWord == F("t1"))	//translate powder bed source
		{
			pos = payload.substring(3).toInt();
			sourceStepper.moveTo(pos);
			sourceTranslatingTo = pos;
			sourceMoving = true;

			sendOkay();
		}

		if (firstWord == F("xs"))
		{
			//xs 1111222233334444
			xTransSpeed = payload.substring(3, 7).toInt();
			xTransAccel = payload.substring(7, 11).toInt();
			xPrintSpeed = payload.substring(11, 15).toInt();
			xPrintAccel = payload.substring(15, 19).toInt();
			
			sendOkayComplete();
		}

		if (firstWord == F("ys"))
		{
			//ys 00001111
			int maxSpeed = payload.substring(3, 7).toInt();
			int maxAccel = payload.substring(7, 11).toInt();
			yStepperA.setMaxSpeed(maxSpeed);
			yStepperB.setMaxSpeed(maxSpeed);
			yStepperA.setAcceleration(maxAccel);
			yStepperB.setAcceleration(maxAccel);

			sendOkayComplete();
		}

		if (firstWord == F("bs"))
		{
			int speed = payload.substring(3).toInt();
			platformStepper.setMaxSpeed(speed);
			sourceStepper.setMaxSpeed(speed);
			
			sendOkayComplete();
		}

		if (firstWord == F("ba"))
		{
			int accel = payload.substring(3).toInt();
			platformStepper.setAcceleration(accel);
			sourceStepper.setAcceleration(accel);
			
			sendOkayComplete();
		}

		if (firstWord == F("xd"))
		{
			xDotDensity = payload.substring(3).toInt();
			
			sendOkayComplete();
		}

		if (firstWord == F("pli"))
		{
			//pli 000xxxxxxxx
			int k = payload.substring(4, 7).toInt();
			k = k * 13;	//since we've broken up the print lines, this is the offset, 14.

			int packetlength = payload.length();
			int beginningOfData = 7;

			int curPrintArrIndex = k;
			for (int i = beginningOfData; i < packetlength; i += 3)
			{
				//curPrintArrIndex = ((i - beginningOfData) / 3) + k;
				printArray[curPrintArrIndex] = (payload.charAt(i) & 15);
				//MS
				printArray[curPrintArrIndex] = printArray[curPrintArrIndex] << 4;
				printArray[curPrintArrIndex] = printArray[curPrintArrIndex] | (payload.charAt(i + 1) & 15);
				printArray[curPrintArrIndex] = printArray[curPrintArrIndex] << 4;
				printArray[curPrintArrIndex] = printArray[curPrintArrIndex] | (payload.charAt(i + 2) & 15);

				curPrintArrIndex++;
			}

			int dummy = freeRam();
			if (testpoint[5] < dummy)
				testpoint[5] = dummy;

			
			sendOkayComplete();
		}

		if (firstWord == F("pi"))
		{
			
			//pi lllllnnnnnnnnd
			//pi 014000000101
			length = payload.substring(3, 8).toInt();
			nextFireLocation = payload.substring(8, 16).toInt();
			char direction = payload.charAt(16);
			if (direction == '0')
				printingDirection = false;
			else
				printingDirection = true;

			printLocation = 0;	//reset this so, when we begin printing, we start from the beginning of the array

			sendOkayComplete();

			int dummy = freeRam();
			if (testpoint[6] < dummy)
				testpoint[6] = dummy;
		}

		if (firstWord == F("t4p"))
		{

			xStepper.setMaxSpeed(xPrintSpeed);
			xStepper.setAcceleration(xPrintAccel);

			pos = payload.substring(4).toInt();	//the position we're translating to

			printing = true;
			xMoving = true;
			xTranslatingTo = pos;
			xStepper.moveTo(pos);

			
			sendOkay();

		}

		if (firstWord == F("inv"))
		{
			//inv 01
			int xInv = payload.charAt(4);
			int yInv = payload.charAt(5);
			int sInv = payload.charAt(6);
			int pInv = payload.charAt(7);
			if (xInv == '1')
				xStepper.setPinsInverted(true, false, false);
			else
				xStepper.setPinsInverted(false, false, false);
			if (yInv == '1')
			{
				yStepperA.setPinsInverted(true, false, false);
				yStepperB.setPinsInverted(true, false, false);
			}
			else
			{
				yStepperA.setPinsInverted(false, false, false);
				yStepperB.setPinsInverted(false, false, false);
			}
			if (sInv == '1')
			{
				sourceStepper.setPinsInverted(true, false, false);
			}
			else
			{
				sourceStepper.setPinsInverted(false, false, false);
			}

			if (pInv == '1')
			{
				platformStepper.setPinsInverted(true, false, false);
			}
			else
			{
				platformStepper.setPinsInverted(false, false, false);
			}

			sendOkayComplete();
		}

		

		if (firstWord == F("rs"))
		{
			rollingStepper.setMaxSpeed(payload.substring(3, 7).toInt());
			rollingStepper.setAcceleration(payload.substring(7, 11).toInt());

			sendOkayComplete();
		}

		if (firstWord == F("rt"))
		{
			rollingTranslateTo = payload.substring(3).toInt();
			rollingStepper.moveTo(rollingTranslateTo);
			rollingMoving = true;

			sendOkay();
		}

	}
	else
	{
		if (payload == F("fireOnce"))	//used for testing
		{
			MyInkShield.spray_ink(0x0FFF);
			
			sendOkayComplete();
		}

		if (payload == F("fireContinuously"))
		{
			firingContinuously = true;
			
			sendOkayComplete();
		}

		if (payload == F("stopContinuously"))
		{
			firingContinuously = false;
			
			sendOkayComplete();
		}

		if (payload == F("stackHeapDistance"))
		{
			//0-7
			String str0, str1, str2, str3, str4, str5, str6, str7, str8;
			str0 = String(testpoint[0]);
			str1 = String(testpoint[1]);
			str2 = String(testpoint[2]);
			str3 = String(testpoint[3]);
			str4 = String(testpoint[4]);
			str5 = String(testpoint[5]);
			str6 = String(testpoint[6]);
			str7 = String(testpoint[7]);
			str8 = String(testpoint[8]);

			String packet = "freeRam " + str0 + " " + str1 + " " + str2 + " " + str3 + " " + str4 + " " + str5 + " " + str6 + " " + str7 + " " + str8;

			sendPacket(packet);

			sendOkayComplete();
		}

		if (payload == F("hb"))
		{
			platformStepper.setCurrentPosition(0);
			sourceStepper.setCurrentPosition(0);

			sendOkayComplete();
		}

		if (payload == F("xyh"))
		{
			xStepper.setCurrentPosition(0);
			yStepperA.setCurrentPosition(0);
			yStepperB.setCurrentPosition(0);

			sendOkayComplete();
		}
	}
}

void notifyFailure(long commandNum)
{
	String strLineNum = String(commandNum);
	sendPacket("f" + strLineNum + " '" + packetBuffer + "'");
}

void sendPacket(String packet)
{
	//String packetWithEndline = packet + '\n';
	Serial.print(packet);
	Serial.print('\n');
	//Serial.flush();
}

int calcChecksum(String cmd)
{
    digitalWrite(13, HIGH);


	int cs = 0;
    int x;
    bool j = false;
	int cmdLength = cmd.length();
    for (int i = 0; i < cmdLength; ++i)
    {
        if (j)
        {
            x = cmd[i];
            x = x << 8;
            cs = cs ^ x;
        }
        else
        {
            cs = cs ^ cmd[i];
        }
        j = !j;
    }

	digitalWrite(13, LOW);

	int dummy = freeRam();
		if (testpoint[7] < dummy)
			testpoint[7] = dummy;
    return cs;
}

int freeRam ()
{
    extern int __heap_start, *__brkval;
    int v;
    return (int) &v - (__brkval == 0 ? (int) &__heap_start : (int) __brkval);
}

void sendOkayComplete()
{
	sendOkay();
	sendComplete();
}

void sendOkay()
{
	sendPacket(F("o"));
}

void sendComplete()
{
	sendPacket(F("c"));
}

