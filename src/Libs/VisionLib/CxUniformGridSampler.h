#include <vector>
#include <unordered_map>
#include <algorithm>
#include <cmath>
#include <limits>
#include <omp.h>
#include "DataType.h"

#pragma once
class CxUniformGridSampler {
public:
	CxUniformGridSampler(float xScale, float yScale, float xMin, float xMax, float yMin, float yMax)
		: m_xScale(xScale), m_yScale(yScale), m_xMin(xMin), m_xMax(xMax), m_yMin(yMin), m_yMax(yMax) {

		m_wdith = static_cast<int>(std::ceil((m_xMax - m_xMin) / m_xScale));
		m_height = static_cast<int>(std::ceil((m_yMax - m_yMin) / m_yScale));
	}

	void processPointCloud(const std::vector<Point3D>& points, const std::vector<uint8_t>& intensity = std::vector<uint8_t>()) {
		// Ԥ����߶�ͼ����
		m_heightMap.resize(m_wdith * m_height, std::numeric_limits<float>::quiet_NaN());
		if (!intensity.empty())
		{
			m_intensityMap.resize(m_wdith * m_height, std::numeric_limits<float>::quiet_NaN());
		}
		else
		{
			m_intensityMap = std::vector<uint8_t>();
		}
		m_pointCount.resize(m_wdith * m_height, 0);

		// ��һ�飺�ۼ�ÿ�����ӵĸ߶�ֵ������ֵ������
#pragma omp parallel for
		for (size_t i = 0; i < points.size(); i++) {
			const Point3D& p = points[i];

			// ��������ڵĸ�������
			int xIdx = static_cast<int>((p.X - m_xMin) / m_xScale);
			int yIdx = static_cast<int>((p.Y - m_yMin) / m_yScale);

			// ����Ƿ�����Ч��Χ��
			if (xIdx >= 0 && xIdx < m_wdith && yIdx >= 0 && yIdx < m_height) {
				int index = yIdx * m_wdith + xIdx;

#pragma omp critical
				{
					// ����ǵ�һ���㣬ֱ�Ӹ�ֵ
					if (m_pointCount[index] == 0) {
						m_heightMap[index] = p.Z;
						if (!intensity.empty()) {
							m_intensityMap[index] = intensity[i];
						}
					}
					else {
						// �����ۼӣ���������ƽ��ֵ��
						m_heightMap[index] += p.Z;
						if (!intensity.empty()) {
							m_intensityMap[index] += intensity[i];
						}
					}
					m_pointCount[index]++;
				}
			}
		}

		// �ڶ��飺����ÿ�����ӵ�ƽ���߶Ⱥ�ƽ������
#pragma omp parallel for
		for (int i = 0; i < m_wdith * m_height; i++) {
			if (m_pointCount[i] > 0) {
				m_heightMap[i] /= m_pointCount[i];
				if (!intensity.empty()) {
					m_intensityMap[i] = static_cast<uint8_t>(m_intensityMap[i] / m_pointCount[i]);
				}
			}
		}
	}

	// ��ѡ������ն���û�е�ĸ��ӣ�
	void fillHoles(int kernelSize = 3) {
		std::vector<float> filledHeightMap = m_heightMap;
		std::vector<uint8_t> filledIntensityMap = m_intensityMap;

#pragma omp parallel for
		for (int y = 0; y < m_height; y++) {
			for (int x = 0; x < m_wdith; x++) {
				int index = y * m_wdith + x;

				// �����ǰ�����ǿյģ�NaN��
				if (std::isnan(m_heightMap[index])) {
					float heightSum = 0.0f;
					float intensitySum = 0.0f;
					int count = 0;

					// ����Χ��kernelSize��kernelSize������Ѱ����Чֵ
					for (int ky = -kernelSize / 2; ky <= kernelSize / 2; ky++) {
						for (int kx = -kernelSize / 2; kx <= kernelSize / 2; kx++) {
							int nx = x + kx;
							int ny = y + ky;

							if (nx >= 0 && nx < m_wdith && ny >= 0 && ny < m_height) {
								int nIndex = ny * m_wdith + nx;
								if (!std::isnan(m_heightMap[nIndex])) {
									heightSum += m_heightMap[nIndex];
									intensitySum += m_intensityMap[nIndex];
									count++;
								}
							}
						}
					}

					// ����ҵ�����Ч���ھӣ������ǵ�ƽ��ֵ���
					if (count > 0) {
						filledHeightMap[index] = heightSum / count;
						filledIntensityMap[index] = static_cast<uint8_t>(intensitySum / count);
					}
				}
			}
		}
		m_heightMap = filledHeightMap;
		m_intensityMap = filledIntensityMap;
	}

	// ��ȡ���
	const std::vector<float>& getHeightMap() const {
		return m_heightMap;
	}

	const std::vector<uint8_t>& getIntensityMap() const {
		return m_intensityMap;
	}

	// ��ȡ�����������������ת��
	float getXScale() const { return m_xScale; }
	float getYScale() const { return m_yScale; }
	float getXOffset() const { return m_xMin; }
	float getYOffset() const { return m_yMin; }
	int getWidth() const { return m_wdith; }
	int getHeight() const { return m_height; }

private:
	float m_xScale, m_yScale;
	float m_xMin, m_xMax, m_yMin, m_yMax;
	int m_wdith, m_height;
	std::vector<float> m_heightMap;
	std::vector<uint8_t> m_intensityMap;
	std::vector<int> m_pointCount;
};