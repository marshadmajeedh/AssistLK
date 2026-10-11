enum LocationSource {
  manual('Manual'),
  openStreetMap('OpenStreetMap');

  final String value;
  const LocationSource(this.value);
  static LocationSource fromJson(dynamic value) =>
      value == 'OpenStreetMap' ? openStreetMap : manual;
}
