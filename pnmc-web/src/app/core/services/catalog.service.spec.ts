import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';
import { CatalogService } from './catalog.service';

describe('CatalogService - cartografia municipal lazy', () => {
  const DEPARTMENTS_OBJECT = 'MGN_ADM_DPTO_POLITICO';
  const MUNICIPALITIES_OBJECT = 'MGN_ADM_MPIO_GRAFICO';

  const createService = (apiClient: unknown) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        CatalogService,
        { provide: ApiClientService, useValue: apiClient },
      ],
    });
    return TestBed.inject(CatalogService);
  };

  const square = (offset: number) => [
    [offset, 0],
    [offset + 0.8, 0],
    [offset + 0.8, 0.8],
    [offset, 0.8],
    [offset, 0],
  ];

  const topologyFixture = () => {
    const municipalityArcReads: Record<string, number> = { '05': 0, '08': 0, '13': 0 };
    let municipalityGeometryReads = 0;

    const municipality = (
      departmentCode: string,
      departmentName: string,
      municipalityCode: string,
      municipalityName: string,
      arcIndex: number,
    ) => {
      const geometry: any = {
        type: 'Polygon',
        properties: {
          dpto_ccdgo: departmentCode,
          dpto_cnmbr: departmentName,
          mpio_cdpmp: municipalityCode,
          mpio_ccdgo: municipalityCode.slice(-3),
          mpio_cnmbr: municipalityName,
        },
      };

      Object.defineProperty(geometry, 'arcs', {
        enumerable: true,
        get: () => {
          municipalityArcReads[departmentCode] += 1;
          return [[arcIndex]];
        },
      });
      return geometry;
    };

    const municipalityGeometries = [
      municipality('05', 'ANTIOQUIA', '05001', 'MEDELLIN', 3),
      municipality('05', 'ANTIOQUIA', '05002', 'ABEJORRAL', 4),
      municipality('08', 'ATLANTICO', '08001', 'BARRANQUILLA', 5),
      municipality('13', 'BOLIVAR', '13001', 'CARTAGENA', 6),
    ];
    const trackedMunicipalityGeometries = new Proxy(municipalityGeometries, {
      get(target, property, receiver) {
        if (typeof property === 'string' && /^\d+$/.test(property)) {
          municipalityGeometryReads += 1;
        }
        return Reflect.get(target, property, receiver);
      },
    });

    const topology = {
      type: 'Topology',
      objects: {
        [DEPARTMENTS_OBJECT]: {
          type: 'GeometryCollection',
          geometries: [
            { type: 'Polygon', arcs: [[0]], properties: { dpto_ccdgo: '05', dpto_cnmbr: 'ANTIOQUIA' } },
            { type: 'Polygon', arcs: [[1]], properties: { dpto_ccdgo: '08', dpto_cnmbr: 'ATLANTICO' } },
            { type: 'Polygon', arcs: [[2]], properties: { dpto_ccdgo: '13', dpto_cnmbr: 'BOLIVAR' } },
          ],
        },
        [MUNICIPALITIES_OBJECT]: {
          type: 'GeometryCollection',
          geometries: trackedMunicipalityGeometries,
        },
      },
      arcs: [
        square(0),
        square(2),
        square(4),
        square(0.1),
        square(0.9),
        square(2.1),
        square(4.1),
      ],
    };

    return {
      topology,
      municipalityArcReads,
      municipalityGeometryReads: () => municipalityGeometryReads,
      resetMunicipalityGeometryReads: () => { municipalityGeometryReads = 0; },
    };
  };

  const loadCatalog = (topology: any) => {
    const apiClient = {
      get: jasmine.createSpy('get').and.returnValue(of(topology)),
    };
    const service = createService(apiClient);
    let result: any;

    service.fetchColombiaGeoJson().subscribe((payload) => { result = payload; });

    return { result, apiClient };
  };

  it('no convierte municipios en la carga nacional y decodifica solo el departamento pedido', () => {
    const fixture = topologyFixture();
    const { result } = loadCatalog(fixture.topology);

    expect(result.features.length).toBe(3);
    expect(result.municipalities.features).toEqual([]);
    expect(Object.values(fixture.municipalityArcReads).reduce((sum, value) => sum + value, 0)).toBe(0);

    fixture.resetMunicipalityGeometryReads();
    const antioquia = result.getMunicipalitiesForDepartment('5');

    expect(fixture.municipalityGeometryReads()).toBe(0);
    expect(antioquia.features.length).toBe(2);
    expect(antioquia.features.every((feature: any) => feature.properties.departmentCode === '05')).toBeTrue();
    expect(antioquia.features.map((feature: any) => feature.properties.municipalityCode)).toEqual([
      '05001',
      '05002',
    ]);
    expect(fixture.municipalityArcReads['05']).toBeGreaterThan(0);
    expect(fixture.municipalityArcReads['08']).toBe(0);
    expect(fixture.municipalityArcReads['13']).toBe(0);
  });

  it('reutiliza la misma coleccion cacheada sin volver a convertir sus arcos', () => {
    const fixture = topologyFixture();
    const { result } = loadCatalog(fixture.topology);

    const first = result.getMunicipalitiesForDepartment('05');
    const readsAfterFirstLoad = fixture.municipalityArcReads['05'];
    const second = result.getMunicipalitiesForDepartment('05');

    expect(second).toBe(first);
    expect(fixture.municipalityArcReads['05']).toBe(readsAfterFirstLoad);
  });

  it('mantiene un LRU acotado y refresca el departamento consultado recientemente', () => {
    const fixture = topologyFixture();
    const { result } = loadCatalog(fixture.topology);

    const antioquia = result.getMunicipalitiesForDepartment('05');
    const atlantico = result.getMunicipalitiesForDepartment('08');
    expect(result.getMunicipalitiesForDepartment('05')).toBe(antioquia);

    result.getMunicipalitiesForDepartment('13');

    expect(result.getMunicipalitiesForDepartment('05')).toBe(antioquia);
    const atlanticoReloaded = result.getMunicipalitiesForDepartment('08');
    expect(atlanticoReloaded).not.toBe(atlantico);
  });

  it('carga el chunk remoto una vez y comparte la Promise y la coleccion normalizada', async () => {
    const fixture = topologyFixture();
    const fullTopology: any = fixture.topology;
    const departmentTopology = {
      ...fullTopology,
      objects: {
        [DEPARTMENTS_OBJECT]: fullTopology.objects[DEPARTMENTS_OBJECT],
      },
    };
    const municipalityTopology = {
      ...fullTopology,
      objects: {
        [MUNICIPALITIES_OBJECT]: {
          ...fullTopology.objects[MUNICIPALITIES_OBJECT],
          geometries: Array.from(fullTopology.objects[MUNICIPALITIES_OBJECT].geometries)
            .filter((geometry: any) => geometry.properties.dpto_ccdgo === '05'),
        },
      },
    };
    const get = jasmine.createSpy('get').and.callFake((url: string) => {
      if (url === '/api/v1/mapa/cartografia/departamentos') return of(departmentTopology);
      if (url === '/api/v1/mapa/cartografia/departamentos/05/municipios') return of(municipalityTopology);
      throw new Error(`Solicitud inesperada: ${url}`);
    });
    const service = createService({ get });
    const catalog = await firstValueFrom(service.fetchColombiaGeoJson());

    const firstRequest = catalog.getMunicipalitiesForDepartment('5');
    const secondRequest = catalog.getMunicipalitiesForDepartment('05');

    expect(secondRequest).toBe(firstRequest);
    const [firstCollection, secondCollection] = await Promise.all([firstRequest, secondRequest]);
    expect(secondCollection).toBe(firstCollection);
    expect(firstCollection.features.length).toBe(2);
    expect(firstCollection.features.map((feature: any) => feature.properties.municipalityCode)).toEqual([
      '05001',
      '05002',
    ]);
    expect(firstCollection.features.every(
      (feature: any) => feature.properties.departmentCode === '05',
    )).toBeTrue();

    expect(catalog.getMunicipalitiesForDepartment('05')).toBe(firstRequest);
    expect(get.calls.allArgs().map(([url]) => url)).toEqual([
      '/api/v1/mapa/cartografia/departamentos',
      '/api/v1/mapa/cartografia/departamentos/05/municipios',
    ]);
  });
});
