import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface ApiHealth {
  api: 'available' | 'unavailable';
  database: 'available' | 'unavailable' | 'not-configured';
}

@Injectable({ providedIn: 'root' })
export class ApiStatusService {
  constructor(private readonly http: HttpClient) {}

  getHealth(): Observable<ApiHealth> {
    return this.http.get<ApiHealth>('/api/health');
  }
}
