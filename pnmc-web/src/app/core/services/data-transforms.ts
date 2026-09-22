export const AGENDA_SHORT_MONTHS = ['ENE', 'FEB', 'MAR', 'ABR', 'MAY', 'JUN', 'JUL', 'AGO', 'SEP', 'OCT', 'NOV', 'DIC'];

export const AGENDA_MONTHS_MAP: Record<string, number> = {
  ENERO: 0,
  FEBRERO: 1,
  MARZO: 2,
  ABRIL: 3,
  MAYO: 4,
  JUNIO: 5,
  JULIO: 6,
  AGOSTO: 7,
  SEPTIEMBRE: 8,
  OCTUBRE: 9,
  NOVIEMBRE: 10,
  DICIEMBRE: 11,
};

export const NEWS_MONTHS_MAP: Record<string, string> = {
  ENE: '01',
  ENERO: '01',
  FEB: '02',
  FEBRERO: '02',
  MAR: '03',
  MARZO: '03',
  ABR: '04',
  ABRIL: '04',
  MAY: '05',
  MAYO: '05',
  JUN: '06',
  JUNIO: '06',
  JUL: '07',
  JULIO: '07',
  AGO: '08',
  AGOSTO: '08',
  SEP: '09',
  SEPT: '09',
  SEPTIEMBRE: '09',
  SET: '09',
  SETIEMBRE: '09',
  OCT: '10',
  OCTUBRE: '10',
  NOV: '11',
  NOVIEMBRE: '11',
  DIC: '12',
  DICIEMBRE: '12',
};

export function parseAgendaTime(timeStr: string): number {
  if (!timeStr) return 0;
  const parts = timeStr.split(' ');
  const time = parts[0] || '0:00';
  const modifier = parts[1] || 'AM';
  
  const timeParts = time.split(':');
  let hours = parseInt(timeParts[0] || '0', 10);
  const minutes = parseInt(timeParts[1] || '0', 10);

  if (hours === 12) {
    hours = modifier === 'AM' ? 0 : 12;
  } else if (modifier === 'PM') {
    hours += 12;
  }

  return hours * 60 + minutes;
}

export function normalizeAgendaTags(rawTags: any): string[] {
  if (Array.isArray(rawTags)) return rawTags.flatMap(normalizeAgendaTags);
  if (typeof rawTags !== 'string') return [];

  return rawTags
    .split(/[,|]/)
    .map((tag) => tag.trim())
    .filter(Boolean);
}

export function agendaRecordHasTag(record: any, targetTag: string): boolean {
  if (!targetTag) return true;
  const tags = normalizeAgendaTags(record?.fields?.Tags);
  return tags.some((tag) => tag.toLowerCase() === targetTag.toLowerCase());
}


export function getNewsDateKeys(dateText = '') {
  const normalizedDate = dateText
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toUpperCase()
    .trim();

  const isoMatch = normalizedDate.match(/(\d{4})[-/](\d{1,2})[-/](\d{1,2})/);
  if (isoMatch) {
    const [, year, month, day] = isoMatch;
    if (year && month && day) {
      const monthKey = `${year}-${month.padStart(2, '0')}`;
      return { dateKey: `${monthKey}-${day.padStart(2, '0')}`, monthKey };
    }
  }

  const numericMatch = normalizedDate.match(/(\d{1,2})[-/](\d{1,2})(?:[-/](\d{2,4}))?/);
  if (numericMatch) {
    const [, day, month, rawYear] = numericMatch;
    if (day && month) {
      const year = rawYear ? rawYear.padStart(4, '20') : '2026';
      const monthKey = `${year}-${month.padStart(2, '0')}`;
      return { dateKey: `${monthKey}-${day.padStart(2, '0')}`, monthKey };
    }
  }

  const dayMonthMatch = normalizedDate.match(/(\d{1,2})\s*(?:DE\s*)?([A-Z]+)(?:\s*(?:DE\s*)?(\d{2,4}))?/);
  if (dayMonthMatch) {
    const [, day, monthText, rawYear] = dayMonthMatch;
    if (day && monthText) {
      const month = NEWS_MONTHS_MAP[monthText];
      if (!month) return null;
      const year = rawYear ? rawYear.padStart(4, '20') : '2026';
      const monthKey = `${year}-${month}`;
      return { dateKey: `${monthKey}-${day.padStart(2, '0')}`, monthKey };
    }
  }

  const monthDayMatch = normalizedDate.match(/([A-Z]+)\s*(\d{1,2})(?:\s*(?:DE\s*)?(\d{2,4}))?/);
  if (monthDayMatch) {
    const [, monthText, day, rawYear] = monthDayMatch;
    if (day && monthText) {
      const month = NEWS_MONTHS_MAP[monthText];
      if (!month) return null;
      const year = rawYear ? rawYear.padStart(4, '20') : '2026';
      const monthKey = `${year}-${month}`;
      return { dateKey: `${monthKey}-${day.padStart(2, '0')}`, monthKey };
    }
  }

  return null;
}


export function splitHeroHeadline(headline = ''): { title: string; titleAccent: string } {
  const normalizedHeadline = (headline || '').trim();
  const words = normalizedHeadline.split(/\s+/).filter(Boolean);

  if (words.length <= 1) {
    return { title: normalizedHeadline, titleAccent: '' };
  }

  let bestSplitIndex = 1;
  let bestDifference = Number.POSITIVE_INFINITY;

  for (let index = 1; index < words.length; index += 1) {
    const left = words.slice(0, index).join(' ');
    const right = words.slice(index).join(' ');
    const difference = Math.abs(left.length - right.length);

    if (difference < bestDifference) {
      bestDifference = difference;
      bestSplitIndex = index;
    }
  }

  return {
    title: words.slice(0, bestSplitIndex).join(' '),
    titleAccent: words.slice(bestSplitIndex).join(' '),
  };
}

export function extractEditorialYears(rawYearValue: any = ''): number[] {
  const yearText = String(rawYearValue || '');
  const matches = [...yearText.matchAll(/\b(19|20)\d{2}\b/g)]
    .map((match) => parseInt(match[0], 10))
    .filter((year) => Number.isFinite(year));

  if (matches.length === 0) return [];

  const extractedYears: number[] = [];

  for (let index = 0; index < matches.length; index += 1) {
    const currentYear = matches[index];
    const nextYear = matches[index + 1];

    if (nextYear) {
      const currentPosition = yearText.indexOf(String(currentYear));
      const nextPosition = yearText.indexOf(String(nextYear), currentPosition + 4);
      const betweenText = nextPosition > currentPosition ? yearText.slice(currentPosition + 4, nextPosition) : '';
      const isRange = /[-–—]/.test(betweenText) && nextYear >= currentYear && nextYear - currentYear <= 10;

      if (isRange) {
        for (let year = currentYear; year <= nextYear; year += 1) {
          extractedYears.push(year);
        }
        index += 1;
        continue;
      }
    }

    extractedYears.push(currentYear);
  }

  return [...new Set(extractedYears)].sort((left, right) => left - right);
}

export function getEditorialSectionIconName(section = ''): string {
  const normalizedSection = section.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toUpperCase();

  if (normalizedSection.includes('FORMACION')) return 'book-open';
  if (normalizedSection.includes('REPERTORIO')) return 'disc';
  if (normalizedSection.includes('INVESTIGACION')) return 'search';
  if (normalizedSection.includes('CREACION') || normalizedSection.includes('CIRCULACION')) return 'music';
  if (normalizedSection.includes('PRODUCCION') || normalizedSection.includes('EMPRENDIMIENTO')) return 'boxes';
  if (normalizedSection.includes('DIVULGACION')) return 'file-video';
  if (normalizedSection.includes('DOTACION') || normalizedSection.includes('INFRAESTRUCTURA')) return 'building';
  if (normalizedSection.includes('INFORMACION')) return 'info';
  if (normalizedSection.includes('GESTION')) return 'landmark';
  return 'library';
}

