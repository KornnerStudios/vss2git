/////////////////////////////////////////////////////////////////////////////
//
//	File: BinaryReader.h
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _BINARY_READER_H_
#define _BINARY_READER_H_


class BinaryReader
{
private:
	u08* m_pData;
	u32  m_DataSize;
	u32  m_Offset;

public:
	BinaryReader(void);
	~BinaryReader(void);

	void Initialize(u08 *pData, u32 dataSize, u32 offset = 0);
	bool TestBytes(u08 bytes[], u32 byteCount);
	bool Skip(u32 byteCount);

	u08  Read08(void);
	u16  Read16(void);
	u32  Read32(void);
	bool ReadData(void *pData, u32 byteCount);

	u16  ComputeCRC(u32 byteCount);

	u32  DataSize(void)			{ return m_DataSize; }
	u32  Offset(void)			{ return m_Offset; }

	void SetOffset(u32 offset)	{ m_Offset = offset; }
	u08* CurrentAddress(void)	{ return m_pData + m_Offset; }
};


#endif // _BINARY_READER_H_


