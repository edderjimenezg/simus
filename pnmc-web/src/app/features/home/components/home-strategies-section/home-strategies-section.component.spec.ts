import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NavigationService } from '../../../../core/services/navigation.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { HomeStrategiesSectionComponent } from './home-strategies-section.component';

describe('HomeStrategiesSectionComponent', () => {
  let fixture: ComponentFixture<HomeStrategiesSectionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomeStrategiesSectionComponent],
      providers: [
        {
          provide: NavigationService,
          useValue: {
            navigate: jasmine.createSpy('navigate'),
            navigateComponent: jasmine.createSpy('navigateComponent'),
          },
        },
        {
          provide: TextosWebService,
          useValue: { getWebText: () => '', getWebImage: () => '', getWebImageAlt: () => '' },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HomeStrategiesSectionComponent);
    fixture.detectChanges();
  });

  afterEach(() => fixture.destroy());

  it('renders strategy metadata as animated text instead of buttons', () => {
    const labels = fixture.nativeElement.querySelectorAll('[data-testid="strategy-label"]') as NodeListOf<HTMLElement>;

    expect(labels.length).toBeGreaterThan(0);
    labels.forEach(label => {
      expect(label.closest('button')).toBeNull();
      expect(label.className).toContain('group-hover:text-[#8BF784]');
      expect(label.className).toContain('group-hover:translate-x-1');
    });
  });
});
