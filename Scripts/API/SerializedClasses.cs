// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
using System;
using System.Collections.Generic;

[Serializable]
public class Artist {
    public int id;
    public string bio;
    public string name;
    public string role;
    public int display_order;
}

[Serializable]
public class Artist2 {
    public string period;
    public int id;
    public string bio;
    public string name;
}

[Serializable]
public class Classification {
    public string period;
    public int id;
    public string @class;
}

[Serializable]
public class Culture {
    public string period;
    public int id;
    public string culture;
}

[Serializable]
public class FiltersDate {
    public string period;
    public int id;
    public string date;
}

[Serializable]
public class ResultsData {
    public string period;
    public int id;
    public string date;
    public string accession_number;
    public string medium;
    public string dimensions;
    public string status;
    public int public_access;
    public string primary_image;
    public int instance_id;
    public string title;
    public int department_id;
    public string department;
    public string signed;
    public string keywords;
    public object condition;
    public string inscribed;
    public string paper_support;
    public object attributes;
    public List<Artist> artists;
}

[Serializable]
public class Department {
    public object period;
    public int id;
    public string dept;
}

[Serializable]
public class Filters {
    public List<Classification> classification;
    public List<Department> department;
    public List<Artist> artist;
    public List<Culture> culture;
    public List<FiltersDate> date;
}

[Serializable]
public class Pagination {
    public int total;
    public int count;
    public int per_page;
    public int current_page;
    public int last_page;
    public int to;
    public int from;
}

[Serializable]
public class Results {
    public List<ResultsData> data;
    public Pagination pagination;
}

[Serializable]
public class MAPData {
    public Results results;
    public Filters filters;
}

