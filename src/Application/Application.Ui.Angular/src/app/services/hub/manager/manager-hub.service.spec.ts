import { TestBed } from '@angular/core/testing';

import { ManagerHubService } from './manager-hub.service';

describe('ManagerHubService', () => {
  let service: ManagerHubService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ManagerHubService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
