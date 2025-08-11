# CxControl ����ṹ����

## 1. ����ṹ
`CxControl` ��Ҫ����3D/2D���ơ����񡢼�����ȿ��ӻ������� OpenGL��SharpGL��ʵ�֡����Ŀؼ�Ϊ `CxDisplay`��������Ⱦ�����������ݹ���

## 2. ��Ҫ�ļ���ְ��

- **CxDisplay.cs**  
  ���ؼ����̳��� `OpenGLControl`��������Ⱦ���̡����ݹ����û���������ꡢ�˵��ȣ�����Э���� RenderItem ����ʾ��

- **Camera/CxAdvancedTrackBallCamera.cs**  
  �ִ���3D�����֧����ת�����š�ƽ�ơ�2D/3D�л���������ͼ�������á�

- **RenderItem/**  
  ��ȾԪ�ػ���͸���ͼԪ���㡢�ߡ��桢Mesh��Box���ı�������ϵ�ȣ���
  - `CxSegment3DItem.cs`��3D�߶�
  - `CxSurfaceItem.cs`������/����
  - `CxMeshItem.cs`����������
  - `CxSurfaceAdvancedItem.cs`�����ģ����/����
  - `CxColorBarItem.cs`��ɫ��
  - `CxCoordinationTagItem.cs`�������ǩ
  - `CxCoordinateSystemItem.cs`������ϵ
  - `CxTextInfoItem.cs`��3D�ı�
  - `CxText2DItem.cs`��2D�ı�
  - `CxPoint3DItem.cs`��3D��
  - `CxBox3DItem.cs`��3D��Χ��

- **ICamera.cs**  
  ����ӿڣ�������ͼ������

- **CxExtension.cs**  
  ��չ�������������ߡ�

- **CxDisplay.Designer.cs / .resx**  
  WinForms ������Զ������ļ���

- **CxControl.csproj**  
  ��Ŀ�ļ���

## 3. �Ƽ�������

- �� RenderItem ֻ��������ͼԪ����Ⱦ����Դ����������չ��
- `CxDisplay` ֻ����������ת����������Ⱦ���ȡ�
- �������Ⱦ������ں���֧�ֶ�����ͼģʽ��
- �����ڽ��鲹�� XML ע�ͺ� `#region`�������ɶ��ԡ�
- ���ڱ��ļ���������ܹ����÷�����չ˵����

---
