/////////////////////////////////////////////////////////////////////////////
//
//	File: CRC32.h
//
//	$Header: /TS/TsFile/CRC32.h 2     2/27/08 7:51p Lee $
//
//
//	This class generates a cumulative CRC value upon the contents of
//	multiple buffers, allowing a CRC value to be generated for data or
//	a file in piecemeal fashion, a block at time.
//
//	For example, this code could be used to generate a CRC value for a
//	file by first instantiating a CRC32 object.  The first few K of
//	the file can then be read into a buffer, and the contents of that
//	buffer passed to AccumulateBuffer().  This would then be repeated,
//	continually reading more data into the buffer and passing that data
//	to AccumulateBuffer() until the end of the file is reached.  The final
//	CRC value can then be obtained by calling RetrieveCRC().  The CRC32
//	object can then be Reset() so that the object can be reused to calculate
//	the CRC value for other data.
//
//	Utility functions:
//		CRC32()
//			Instantiate a new CRC object, initializing it for immediate use.
//
//		Reset()
//			Reset the current incremental CRC value in preparation for
//			calculating a new CRC for a new group of data.
//
//		RetrieveCRC()
//			Retrieve the current CRC value contained in the object.
//
//		AccumulateBuffer()
//			Given a buffer of data, update the current CRC value across
//			all of the data contained in this buffer.  This function will
//			usually be called multiple times for each block of data within
//			the file or data structure for which the CRC is being generated.
//			NOTE: It is vital that data which is passed to this function
//			always be passed in the same order!  If the blocks of data are
//			passed in a different order, then a different CRC value will be
//			generated.
//
//	Note: This code is based upon sample code written by Tomi Mikkonen
//	(tomitm@remedy.fi), including the CRC algorithm and the look-up table.
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _CRC_32_H_
#define _CRC_32_H_

class CRC32
{
private:
	u32 m_CRC;

public:
	CRC32(void);

	void Reset(void);
	u32  RetrieveCRC(void);
	void AccumulateBuffer(u08 buffer[], u32 bufferSize);
};


u32 VssCrc32(u08 buffer[], u32 bufferSize);
u16 VssCrc16(u08 buffer[], u32 bufferSize);


#endif // _CRC_32_H_



