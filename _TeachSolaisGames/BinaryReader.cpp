/////////////////////////////////////////////////////////////////////////////
//
//	File: BinaryReader.cpp
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#include "Common.h"
#include "BinaryReader.h"
#include "CRC32.h"


#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif


/////////////////////////////////////////////////////////////////////////////
//
//	constructor
//
BinaryReader::BinaryReader(void)
	:	m_pData(NULL),
		m_DataSize(0),
		m_Offset(0)
{
}


/////////////////////////////////////////////////////////////////////////////
//
//	destructor
//
BinaryReader::~BinaryReader(void)
{
}


/////////////////////////////////////////////////////////////////////////////
//
//	Free()
//
void BinaryReader::Initialize(u08 *pData, u32 dataSize, u32 offset)
{
	m_pData    = pData;
	m_DataSize = dataSize;
	m_Offset   = (offset < dataSize) ? offset : dataSize;
}


/////////////////////////////////////////////////////////////////////////////
//
//	TestBytes()
//
bool BinaryReader::TestBytes(u08 bytes[], u32 byteCount)
{
	if ((m_Offset + byteCount) > m_DataSize) {
		return false;
	}

	for (u32 i = 0; i < byteCount; ++i) {
		if (bytes[i] != m_pData[i+m_Offset]) {
			return false;
		}
	}

	return true;
}


/////////////////////////////////////////////////////////////////////////////
//
//	Skip()
//
bool BinaryReader::Skip(u32 byteCount)
{
	if ((byteCount + m_Offset) > m_DataSize) {
		m_Offset = m_DataSize;
		return false;
	}

	m_Offset += byteCount;

	return true;
}


/////////////////////////////////////////////////////////////////////////////
//
//	Read08()
//
u08 BinaryReader::Read08(void)
{
	if (m_Offset >= m_DataSize) {
		return 0;
	}

	return m_pData[m_Offset++];
}


/////////////////////////////////////////////////////////////////////////////
//
//	Read16()
//
u16 BinaryReader::Read16(void)
{
	if ((m_Offset + 2) > m_DataSize) {
		m_Offset = m_DataSize;
		return 0;
	}

	u16 a = m_pData[m_Offset++];
	u16 b = m_pData[m_Offset++];

	return (a | (b << 8));
}


/////////////////////////////////////////////////////////////////////////////
//
//	Read32()
//
u32 BinaryReader::Read32(void)
{
	if ((m_Offset + 2) > m_DataSize) {
		m_Offset = m_DataSize;
		return 0;
	}

	u32 a = m_pData[m_Offset++];
	u32 b = m_pData[m_Offset++];
	u32 c = m_pData[m_Offset++];
	u32 d = m_pData[m_Offset++];

	return (a | (b << 8) | (c << 16) | (d << 24));
}


/////////////////////////////////////////////////////////////////////////////
//
//	ReadData()
//
bool BinaryReader::ReadData(void *pData, u32 byteCount)
{
	if ((m_Offset + byteCount) > m_DataSize) {
		m_Offset = m_DataSize;
		for (u32 i = 0; i < byteCount; ++i) {
			reinterpret_cast<u08*>(pData)[i] = 0;
		}
		return false;
	}

	for (u32 i = 0; i < byteCount; ++i) {
		reinterpret_cast<u08*>(pData)[i] = m_pData[i + m_Offset];
	}

	m_Offset += byteCount;

	return true;
}


/////////////////////////////////////////////////////////////////////////////
//
//	ComputeCRC()
//
//	This is called at the start of each chunk, after the size, marker, and
//	16-bit CRC.  This will compute the 16-bit CRC for the chunk, so it can
//	be tested.  Note that VSS may store the CRC as zero, which indicates
//	that the CRC is unknown, and should be ignored.
//
u16 BinaryReader::ComputeCRC(u32 byteCount)
{
	if ((byteCount + m_Offset) > m_DataSize) {
		return 0;
	}

	return VssCrc16(m_pData + m_Offset, byteCount);
}








