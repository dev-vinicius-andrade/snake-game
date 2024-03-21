import { TestBed } from '@angular/core/testing';

import { KeyboardEventHandlerService } from './keyboard-event-handler.service';

describe('KeyboardEventHandlerService', () => {
  let service: KeyboardEventHandlerService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(KeyboardEventHandlerService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
