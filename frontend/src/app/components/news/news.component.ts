// components/news/news.component.ts
// Displays the latest headlines from FT and AP. Each headline links out.

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { NewsItem } from '../../models/models';

@Component({
  selector: 'app-news',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './news.component.html',
  styleUrls: ['./news.component.css']
})
export class NewsComponent implements OnInit {
  news: NewsItem[] = [];
  loading = true;

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.api.getNews().subscribe({
      next: data => { this.news = data; this.loading = false; },
      error: () => this.loading = false
    });
  }

  // Ordered list of sources as they arrive from the API (preserves feed order).
  get sources(): string[] {
    const seen = new Set<string>();
    return this.news
      .map(n => n.source)
      .filter(s => { if (seen.has(s)) return false; seen.add(s); return true; });
  }

  bySource(source: string): NewsItem[] {
    return this.news.filter(n => n.source === source);
  }
}
